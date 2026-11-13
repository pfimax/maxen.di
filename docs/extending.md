# Extending Maxen.DI

Everything the container does by default is built from the same pieces you can use yourself.

## How a resolve flows

```
Validate (loops, captive dependencies)
 -> pre-lifetime middleware      runs on every resolve
 -> lifetime                     decides whether to reuse or build
 -> post-lifetime middleware     runs only when something new is built
 -> decorators                   lowest priority sits closest to the service
 -> activation                   constructor, then initializers
```

A singleton that already exists skips all of this, unless its registration has pre-lifetime middleware.

## Writing a lifetime

```csharp
public sealed class PerTenantLifetime : Lifetime
{
    public override string Name => "PerTenant";
    // longer-lived services must not capture it
    public override bool IsScopeBound => true;

    public override object Resolve(
        ResolveContext context, ResolveDelegate next)
        => context.Scope.Instances.GetOrCreate(
            (context.Descriptor, TenantId(context)),
            () => next(context),
            context.Descriptor.OwnsInstance);
}

builder.Register<IReport, Report>().WithLifetime(new PerTenantLifetime());
```

`InstanceStore` is what scopes use to cache instances and to dispose them (newest first) when the scope ends.

## Middleware

```csharp
builder.Use(MiddlewarePhase.PreLifetime, (ctx, next) =>
{
    Console.WriteLine($"resolving {ctx.Key}");
    return next(ctx);
});
```

`PostLifetime` middleware only sees newly built instances, so it is the place to wrap or audit creation.

## The other extension points

| Point | Interface | Added with |
|---|---|---|
| On-the-fly registrations (like `Lazy<T>`) | `IRegistrationSource` | `AddSource` |
| Picking a constructor | `IConstructorSelector` | `ConstructorSelector = ...` |
| Filling constructor parameters | `IParameterResolver` | `AddParameterResolver` |
| Post-construction steps | `IInstanceInitializer` | `AddInitializer` |
| Method interception | `IInterceptor` | `.InterceptWith(...)`, `builder.Intercept<,>()` |
| Watching resolves | `IResolutionObserver` | `Observe` |
| Reusable setup | `IContainerModule` | `RegisterModule` |

A factory registration is a closed box to validation and the dependency graph. If you write factories, or generate
registrations with a tool of your own, say what each one resolves and it is checked like a constructor:

```csharp
builder.RegisterFactory<IGreeter>(
        ctx => new Greeter(ctx.Resolve<IClock>()))
    .WithMetadata(ServiceMetadata.Dependencies,
        new[] { ServiceKey.For<IClock>() });
```

Use `ServiceMetadata.SameKey` as the key of an entry to mean "the key this registration has itself".

Adding a custom `IParameterResolver` switches the container from its fastest construction path to the general
one, because your resolver might claim any parameter. That costs some speed per construction; it is still correct.

## Trimming and native AOT

The core library is analyzed for trimming and AOT (`IsAotCompatible`). Registrations made with
`Register<T, TImpl>()`, factories, instances and the `[Service]` source generator are safe. Features that
inherently use reflection are marked `[RequiresUnreferencedCode]` so you are warned when you use them:
assembly scanning, `[Inject]` properties and methods, `InterceptWith<T>()` and `Intercept<,>()`, and binding options from dictionaries or
configuration. Open-generic registrations work for closed types your app actually uses.
The console sample publishes with native AOT and runs; a CI job does that on Linux with warnings treated as errors on
every push. It also publishes cleanly with the IL trimmer.

The source generator also writes constructor calls for the types you register in code and stores them in
`GeneratedActivators`. The container prefers them over compiled expression trees, which matters most on native AOT.
Without one, a constructor is called through reflection at first and gets a compiled call, made on a background
thread, after its second use; [the source generator](source-generator.md#generated-activators-for-types-registered-in-code)
has the details.
If you generate code with your own tool, you can add activators the same way:

```csharp
GeneratedActivators.Add(
    typeof(Foo),
    new[] { typeof(IClock) },
    static args => new Foo((IClock)args[0]!));
```
