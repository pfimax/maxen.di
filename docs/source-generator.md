# The source generator

Maxen.DI comes with a source generator that runs while your project compiles. It turns classes marked with attributes into
plain `new Foo(...)` registrations, writes constructor calls for the types you register in code, and writes interface
proxies for interception, so the container needs less reflection at run time and works with trimming and native AOT.

## Nothing to install

The generator, the analyzers and their code fixes ship inside the `Maxen.DI` package, under `analyzers/`. Once your
project references `Maxen.DI`, the generator runs on every build. There is no switch to turn on and no second package.

It does three separate jobs, and you can use any of them on their own:

- registrations from `[Service]`, `[Decorator]` and `[Intercept]` classes, applied with `RegisterGeneratedServices()`;
- constructor calls for types you register in code with `Register<T>()`, used automatically;
- proxy classes for the interfaces you intercept, also used automatically.

The [generator sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Generators) shows all
three running together.

## Registering a class with `[Service]`

Put <xref:Maxen.DI.ServiceAttribute> on a class to say how it should be registered:

```csharp
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IPriceCalculator))]
public sealed class StandardPricing : IPriceCalculator
{
    public decimal PriceFor(decimal basePrice) => basePrice;
}

[Service(ServiceLifetime.Singleton, ServiceType = typeof(IStorage),
    Key = "disk")]
public sealed class DiskStorage : IStorage { /* ... */ }
```

The attribute has three settings, and no others:

| Setting | Meaning |
|---|---|
| lifetime (constructor argument) | `ServiceLifetime.Transient` (the default), `Scoped` or `Singleton`. |
| `ServiceType` | The type to register as. Leave it out to register the class as itself. |
| `Key` | A key for keyed lookups, such as `"disk"`. Any constant works: a string, a number, an enum value or a `typeof`. |

Each class gets one registration. For the other lifetimes (`AsScopedTo`, `AsPooled`, `AsPerThread`), conditions, or a
class that should answer to several service types, register it in code instead; see [Registering services](registering.md).

If the class does not implement the `ServiceType` you name, the build fails with `MAXEN003`.

### Applying the registrations

The generator writes one extension method per project, `RegisterGeneratedServices()`. Call it on the builder:

```csharp
var builder = new ContainerBuilder();
builder.RegisterGeneratedServices();
builder.Register<IClock, SystemClock>().AsSingleton();

await using var container = builder.Build();
```

The method is `internal`, so each project registers its own classes; a library that uses `[Service]` needs to expose
a public method of its own that calls it. It lives in a namespace named after the assembly (`MyApp.Core` for an assembly
called `MyApp.Core`; characters that can't appear in a namespace become `_`). Code inside that namespace, or one nested in it, sees
it straight away. Code elsewhere, including top-level statements in `Program.cs`, needs a `using MyApp.Core;`.

The registrations go in when you call the method, in a fixed order (by class name, decorators last). Like any other
registration, the last one of a service wins, so call `RegisterGeneratedServices()` before anything you want to override
it with.

## How the constructor and its parameters are chosen

The generator picks the constructor the same way you would read it:

1. a constructor marked <xref:Maxen.DI.InjectAttribute>, if there is one;
2. otherwise the one with the most parameters.

It considers public and internal constructors. If two constructors tie for the most parameters, it reports `MAXEN001`
and leaves the class out; mark the one you mean with `[Inject]`.

The choice is made at compile time. The runtime container, by contrast, picks the greediest constructor it can satisfy
with what is registered. A generated registration always calls the constructor the generator chose, so if one of its
dependencies is missing, resolving the class fails instead of falling back to a smaller constructor.

Each parameter is resolved from the container. These attributes change what it asks for:

| On the parameter | What it gets |
|---|---|
| `[FromKey("fast")]` | The service registered with the key `"fast"`. |
| `[FromKeyedServices("fast")]` | The same, with Microsoft's attribute. |
| `[FromKeyedServices]` (no key) | The service registered under the same key as the class being built. |
| `[FromKeyedServices(null)]` | The unkeyed service. |
| `[ServiceKey]` | Not a service: the key this class was registered with. |

`[FromKey]` is <xref:Maxen.DI.FromKeyAttribute>. `[FromKeyedServices]` and `[ServiceKey]` come from
`Microsoft.Extensions.DependencyInjection.Abstractions`; the generator recognises them by name, so a project that
never references that package is unaffected.

```csharp
[Service(Key = "fast")]
public class ReportWriter
{
    public ReportWriter(
        [ServiceKey] string key,
        // the clock registered under "fast", like this class
        [FromKeyedServices] IClock sameKeyClock,
        [FromKeyedServices(null)] IClock plainClock)
    {
        // ...
    }
}
```

## What the generator can't handle

Some classes can't be written as a plain `new Foo(...)` call. The generator skips them and tells you why:

- generic, abstract and static classes;
- a class without a public or internal constructor;
- constructor parameters that are optional (have a default value) or passed by reference (`ref`, `out`, `in`);
- `[Intercept]` on a class whose service type is not an interface, or with an interceptor that has no public
  parameterless constructor.

| ID | When |
|---|---|
| `MAXEN001` | Two constructors tie for the most parameters. |
| `MAXEN002` | The class is one of the cases above. |
| `MAXEN003` | The class doesn't implement its `ServiceType` (or its `[Decorator]` service). An error. |
| `MAXEN004` | A `[Service]` singleton takes a `[Service]` class that is scoped. |
| `MAXEN008` | A `[Decorator]` class has no constructor that takes the service it wraps. |

The [diagnostics reference](diagnostics.md) explains each one and how to fix it.

A generated registration is a factory registration, so the container treats it as opaque: run-time validation and the
`maxen-di` tool can't see its dependencies. `MAXEN004` is how the generator makes up for that, at compile time.

### `RegisterAttributed`, the run-time fallback

`builder.RegisterAttributed(assembly)` reads the same attributes by reflection when the container is built. It handles
the classes the generator skips, such as ones with optional constructor parameters, and applies `[Decorator]` and `[Intercept]` too:

```csharp
builder.RegisterAttributed(typeof(Program).Assembly);
```

It is marked `[RequiresUnreferencedCode]`, because it scans the assembly. Use it for one assembly or the other, not
together with `RegisterGeneratedServices()` on the same assembly: that registers every class twice and wraps every
decorator twice.

## Decorators with `[Decorator]`

<xref:Maxen.DI.DecoratorAttribute> wraps every registration of a service, like `builder.Decorate(...)` does, without
the call. The decorator takes the service it wraps as a constructor parameter:

```csharp
[Decorator(typeof(IPriceCalculator), Priority = 10)]
public sealed class TaxPricing : IPriceCalculator
{
    private readonly IPriceCalculator _inner;

    public TaxPricing(IPriceCalculator inner) => _inner = inner;

    public decimal PriceFor(decimal basePrice)
        => _inner.PriceFor(basePrice) * 1.2m;
}
```

`Priority` sets the stacking order: lower numbers sit closer to the real service, and decorators with the same priority
stack in registration order. The default is 0.

Open generics work. A generic decorator pairs with an open service type:

```csharp
[Decorator(typeof(IShelf<>))]
public sealed class LabelledShelf<T> : IShelf<T>
{
    private readonly IShelf<T> _inner;

    public LabelledShelf(IShelf<T> inner) => _inner = inner;

    public string Label() => "[" + _inner.Label() + "]";
}
```

A generic decorator needs an open service such as `typeof(IShelf<>)`, and a plain decorator a closed one; mixing them
gives `MAXEN002`. A decorator class doesn't need `[Service]` itself. More on decorators in
[Decorators and interception](decorators-and-interception.md).

## Interceptors with `[Intercept]`

<xref:Maxen.DI.InterceptAttribute> puts an interceptor in front of a `[Service]` class:

```csharp
[Service(ServiceLifetime.Scoped, ServiceType = typeof(IInventory))]
[Intercept(typeof(CallCounter), Order = 1)]
[Intercept(typeof(AuditTrail), Order = 2)]
public sealed class Inventory : IInventory
{
    public int Count(string item) => item.Length;
}

public sealed class CallCounter : IInterceptor
{
    public static int Calls;

    public void Intercept(IInvocation invocation)
    {
        Interlocked.Increment(ref Calls);
        invocation.Proceed();
    }
}
```

The rules:

- The service type must be an interface, so set `ServiceType`.
- The interceptor needs a public parameterless constructor. It is created once, when `RegisterGeneratedServices()` runs,
  and that one instance handles every call, so it can't take dependencies and must be safe to share between threads.
- Lower `Order` runs first, on the outside. Equal numbers keep the order they are written in. The default is 0.

The proxy class is written at compile time, so nothing is generated at run time and it works with native AOT. The
generator also writes proxies for interfaces you intercept in code with `Intercept<TService, TInterceptor>()` or
`InterceptWith(...)`. It can't write one for an interface with generic methods, `ref` or `out` parameters, events,
indexers or static members; those fall back to the run-time proxy, which native AOT doesn't support.

## Generated activators for types registered in code

For every type you register with a generic `Register` call, such as `Register<Plain>()` or
`Register<IFoo, Foo>()`, the generator writes a direct constructor call for each public constructor. A module
initializer hands them to <xref:Maxen.DI.GeneratedActivators> when the assembly loads. You don't call anything.

```csharp
// The generator sees this call and writes "new Plain()" for it.
builder.Register<Plain>();
```

These calls replace the expression trees the container would otherwise build and compile for those constructors. How
much they replace depends on the runtime:

- Under native AOT, compiled expression trees run in a slow interpreter, so the generated calls are used for every
  object the container builds.
- On a normal runtime, the container keeps its own compiled fast path for most constructors, because it needs no
  argument array. The generated call takes over where the container would otherwise compile an array-based call.

`GeneratedActivators.PreferForAllConstruction` is the switch between the two. It is `true` when the runtime can't
compile dynamic code, which means native AOT, and `false` otherwise. It is public and settable, for tests and special
hosts; you shouldn't need to touch it.

Types are skipped when the generated code couldn't call them: abstract, static or open generic classes, types the
project can't see, and constructors with `ref`, pointer or `ref struct` parameters. Non-generic calls such as
`Register(typeof(IFoo), typeof(Foo))`, and registration methods with other names, get no activator. A skipped type is
built the usual way.

## Trimming and native AOT

What is safe with the trimmer and native AOT:

- `Register<TService, TImplementation>()`, factories and instances;
- everything `RegisterGeneratedServices()` registers, including `[Decorator]` and `[Intercept]`;
- `InterceptWith(...)` with an interceptor instance, when the generator wrote the proxy.

These are marked `[RequiresUnreferencedCode]`, so the trim analyzer warns where you use them:

- assembly scanning: `Scan`, `RegisterAssemblyTypes`, `RegisterAttributed` and `RegisterModules`;
- `UsePropertyInjection()` and `UseMethodInjection()`, for `[Inject]` properties and methods;
- `ResolveWith<T>(object)` and `ResolveArguments.FromObject`, which read an anonymous object;
- `InterceptWith<TInterceptor>()` and `Intercept<TService, TInterceptor>()`, which are also marked
  `[RequiresDynamicCode]`;
- `BindOptions`, from a dictionary or from `IConfiguration`.

The console sample uses the generator and is published with native AOT on Linux in CI, with warnings treated as errors,
and the result is run.

## Reading the generated code

Two MSBuild properties write the generated files to disk. Pass them to one build:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath=obj/generated
```

or set `EmitCompilerGeneratedFiles` to `true` and `CompilerGeneratedFilesOutputPath` to `obj/generated` in a
`<PropertyGroup>` of your project file to keep them on. The generator sample has both lines in its project file,
commented out.

After a build, look under `obj/generated`, in a folder named after the generator. Visual Studio and Rider also list the
files under the project's analyzers node without this setting. There are up to three files: `MaxenGeneratedRegistrations.g.cs` with
`RegisterGeneratedServices()`, `MaxenGeneratedActivators.g.cs` with the constructor calls, and
`MaxenGeneratedProxies.g.cs` with the proxy classes. They are regenerated on every build, so don't edit them.

## The analyzers in the same package

The package also brings analyzers that read the registrations you write in code: a singleton that takes a scoped
service (`MAXEN005`), a constructor that needs a service nothing registers (`MAXEN006`), a disposable transient resolved
from the root container (`MAXEN007`), and a `Lazy` or `Func` of a scoped service inside a singleton (`MAXEN009`).
Several of them come with code fixes.

`MAXEN006` counts your `[Service]` classes as registered, so calling `RegisterGeneratedServices()` keeps it working.
It only works while it can see every registration. It switches itself off for the whole project as soon as it
finds a call it can't follow: scanning, `RegisterAttributed`, modules, profiles,
registration sources, `UseMaxenDI` or a non-generic `Register(typeof(...), ...)`. The
[diagnostics reference](diagnostics.md) lists every ID with its fix, and
[Validation and diagnostics](validation-and-diagnostics.md) covers the checks that run when the container is built.
