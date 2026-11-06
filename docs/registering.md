# Registering services

Everything the container can build starts as a registration on a <xref:Maxen.DI.ContainerBuilder>. This page covers
the ways to register a service, how to pick between several registrations of the same service, and what the container
gives you for free on top of them: collections, `Lazy<T>` and `Func<T>`.

## Types, instances and factories

The everyday form maps a service type to the class that implements it:

```csharp
var builder = new ContainerBuilder();

// Asking for IMailer builds a SmtpMailer.
builder.Register<IMailer, SmtpMailer>();

// A class registered as itself.
builder.Register<OrderService>();

// The same without generics.
builder.Register(typeof(IClock), typeof(SystemClock));
```

The non-generic `Register(Type, Type)` does the same thing for types you only know at runtime, and it is how you
register [open generics](#open-generics). The implementation must be a concrete class that can be assigned to the
service type, otherwise `Register` throws a `RegistrationException` straight away.

Every `Register` call returns a <xref:Maxen.DI.RegistrationBuilder>, where you set the lifetime, a key and the other
options on this page. Without a lifetime the registration is transient. [Lifetimes, scopes and disposal](lifetimes.md)
covers the rest.

When you already have the object, register the instance:

```csharp
var settings = LoadSettings();
builder.RegisterInstance<IAppSettings>(settings);
```

The container hands out that same object every time and never disposes it, because you created it. Disposing it is
your job.

When building the service takes more than a constructor call, register a factory. It receives a `ResolveContext`,
which you use to resolve the factory's own dependencies:

```csharp
builder.RegisterFactory<IDbConnection>(ctx =>
{
    var settings = ctx.Resolve<IAppSettings>();
    return new SqlConnection(settings.ConnectionString);
}).AsScoped();
```

A factory must not return null. Factories can't be registered for open generic services, because the delegate has no
way of knowing which closed type it is being asked for.

Objects the container builds, through a constructor or a factory, are disposed by the container when their scope
ends. If something else owns them, say so:

```csharp
builder.RegisterFactory<HttpClient>(_ => clientPool.Rent())
    .ExternallyOwned();
```

See the [Registration sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Registration).

## Changing what is already registered

Registering the same service twice is allowed. The last registration wins when you ask for one service, and both show
up when you ask for all of them. When you want to be explicit about changing a registration, for example in a test
setup or a module that adjusts another module's defaults, use one of these:

```csharp
// Only registers if nothing is registered for IMailer yet.
builder.TryRegister<IMailer, SmtpMailer>()?.AsSingleton();

// Removes every earlier IMailer registration, then registers.
builder.Replace<IMailer, ConsoleMailer>();

// Registers on top: wins for Resolve, original kept for ResolveAll.
builder.Override<IMailer, AuditedMailer>();
```

`TryRegister` returns null when it did nothing, hence the `?.` before the lifetime. It is the call to use in a library
that wants to supply a default the application can still change.

`Replace` and `Override` both fail with a `RegistrationException` when nothing was registered before. That is on
purpose: a typo in the service type can't quietly leave the original in place. The difference between them only shows
when you resolve every registration. After `Replace` there is one `IMailer`; after `Override` there are two, and the
new one is the one a constructor receives. `ReplaceFactory` and `ReplaceInstance` work like `Replace` with a factory or
an existing object.

All four only consider unkeyed registrations. `TryRegister` and `Override` also count an open generic that covers the
type, while `Replace` only removes registrations of exactly that service type.

## One instance under several service types

A class often implements more than one interface. Registering it twice as a singleton gives you two singletons, one
per registration. `AlsoAs` exposes one registration under extra service types, so they share the instance:

```csharp
builder.Register<IReader, FileStore>()
    .AlsoAs<IWriter>()
    .AsSingleton();

// Same object either way.
var reader = scope.Resolve<IReader>();
var writer = scope.Resolve<IWriter>();
```

The extra type must be implemented by the class. `AlsoAs` is not supported for open generic registrations.

## Keyed services

A key picks one of several registrations of the same service, for example two mailers that talk to different servers:

```csharp
builder.Register<IMailer, SmtpMailer>().WithKey("primary");
builder.Register<IMailer, BackupMailer>().WithKey("backup");

var mailer = scope.Resolve<IMailer>("primary");
```

Any object can be a key: a string, an enum value, a number. A keyed registration only answers to its key, so
`Resolve<IMailer>()` without a key does not find either of the two above.

In a constructor, mark the parameter with <xref:Maxen.DI.FromKeyAttribute>:

```csharp
public class Newsletter
{
    public Newsletter([FromKey("backup")] IMailer mailer) { }
}
```

Classes written for Microsoft's container work unchanged. Maxen.DI reads Microsoft's `[FromKeyedServices("backup")]`
the same way as `[FromKey("backup")]`. `[FromKeyedServices]` with no key (version 10 of Microsoft's packages) asks for
the registration with the same key as the service being built. A parameter marked `[ServiceKey]` receives the key
itself rather than a service. Both attributes come from `Microsoft.Extensions.DependencyInjection.Abstractions`; the
core library reads them by name and takes no dependency on that package.

### One registration for every key

When you can't list the keys up front, such as one database per tenant name, register with `ServiceKey.AnyKey`:

```csharp
builder.Register<ITenantDb, TenantDb>()
    .WithKey(ServiceKey.AnyKey)
    .AsSingleton();

public class TenantDb : ITenantDb
{
    public TenantDb([ServiceKey] string tenant) { }
}

var acme = scope.Resolve<ITenantDb>("acme");
var globex = scope.Resolve<ITenantDb>("globex");
```

Each key is treated as a registration of its own, so the singleton above is one instance per key, and the `[ServiceKey]`
parameter tells each instance which key it was built for. A registration made with a specific key, such as
`.WithKey("acme")`, always wins over `AnyKey`, whichever was registered first.

`AnyKey` does not answer unkeyed requests. Resolving a single service with `AnyKey` as the key throws, because there is
no one service to return. Resolving `IEnumerable<ITenantDb>` with it returns every registration that has a specific
key, leaving out the unkeyed ones and the `AnyKey` registration itself.

If a `[ServiceKey]` parameter can't take the key (the service was registered without one, or the key has a different
type), the container uses another constructor if there is one, and otherwise throws a `ResolutionException` that
names the parameter.

See the [Keyed sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Keyed).

## Open generics

Register a generic definition once and the container builds the closed type you ask for:

```csharp
builder.Register(typeof(IRepository<>), typeof(Repository<>))
    .AsScoped();

// Builds a Repository<Customer>.
var customers = scope.Resolve<IRepository<Customer>>();
```

Each closed type is a registration of its own, so `IRepository<Customer>` and `IRepository<Order>` get separate
instances. If the implementation's generic constraints rule out a type argument, the open registration doesn't apply to
it.

A closed registration you write out yourself is more specific, so it wins over the open generic for that one type,
whatever the registration order:

```csharp
builder.Register<IRepository<Customer>, CachedCustomerRepository>();
```

When you resolve all of them, the closed registrations and the closed forms of open generics are merged in
registration order.

See the [Generics sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Generics).

## Collections

Ask for a collection and you get every registration of the element type, in registration order:

```csharp
builder.Register<IHealthCheck, DatabaseCheck>();
builder.Register<IHealthCheck, DiskCheck>();

public class HealthReport
{
    public HealthReport(IEnumerable<IHealthCheck> checks) { }
}

var checks = scope.ResolveAll<IHealthCheck>();
```

`IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IList<T>` and `T[]` all work, as
constructor parameters or with `Resolve`. Each member keeps its own lifetime. An empty collection is a valid answer,
so asking for a list of something nobody registered never fails. With a key, the collection gathers the registrations
made under that key.

A read-only shape (`IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`) whose members are all
unconditional singletons is built once and shared. Arrays, `IList<T>` and `ICollection<T>` are a fresh copy on every
resolve, since they invite changes.

See the [Collections sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Collections).

## `Lazy<T>` and `Func<T>`

For any registered `T`, you can also take a `Lazy<T>` or a `Func<T>` without registering anything else:

```csharp
public class ReportJob
{
    public ReportJob(Lazy<IPdfRenderer> renderer,
                     Func<IReportWriter> newWriter) { }
}
```

`Lazy<T>` builds the service the first time you read `.Value`. Use it for something expensive that a code path may
never need. `Func<T>` asks the container again on every call, so a transient gives you a new instance each time and a
singleton gives you the same one. Both resolve from the scope that was active when the `Lazy` or `Func` itself was
created. If `T` is not registered, neither is offered, so a constructor that needs one counts as unresolvable.

A `Func` can also pass values to the constructor, for parameters that can't come from registrations. `Func<A, T>` up
to `Func<A, B, C, D, T>` are supported, so four arguments at most:

```csharp
builder.Register<IClock, SystemClock>();
builder.Register<Report>().WithRuntimeArguments();

public class Report
{
    public Report(IClock clock, string title, int pages) { }
}

var makeReport = scope.Resolve<Func<string, int, Report>>();
var report = makeReport("Q3", 12);
```

Values are matched to parameters by type, and by position among parameters of the same type. They go to the service
you asked for only, never to its dependencies.

See the [Deferred sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Deferred).

## Arguments at resolve time

`ResolveWith` does the same as a `Func` with arguments, without the delegate, and matches values by parameter name
(ignoring case):

```csharp
var report = scope.ResolveWith<Report>(new { title = "Q3", pages = 12 });
```

The anonymous object is read by reflection. When you trim or publish with native AOT, build the values with
<xref:Maxen.DI.Arguments.ResolveArguments> instead, which can match by name or by type:

```csharp
var arguments = new ResolveArguments()
    .Add("title", "Q3")
    .Add("pages", 12);
var report = scope.ResolveWith<Report>(arguments);
```

Only transient services accept arguments. A cached instance can't depend on values from one call, so `ResolveWith` on
a scoped or singleton service throws a `ResolutionException`.

`WithRuntimeArguments()` on the registration tells validation that some parameters arrive at resolve time, so it does
not report them as missing. It changes nothing else; see [Validation and diagnostics](validation-and-diagnostics.md).

## Conditional registrations

Sometimes one consumer needs a different implementation from everybody else. `WhenInjectedInto` limits a registration
to services being built for one class (or a subclass of it):

```csharp
builder.Register<IClock, SystemClock>();
builder.Register<IClock, FrozenClock>()
    .WhenInjectedInto<ReportJob>();
```

`ReportJob` gets the `FrozenClock`. Everyone else, including a direct `Resolve<IClock>()`, gets the `SystemClock`. When
a conditional registration declines, the container falls back to the next most recent registration, and if none
applies, the service counts as not registered.

`When` takes any check. It receives the `ResolveContext` of the service that is asking, or null when the caller
resolved directly:

```csharp
builder.Register<IClock, FrozenClock>()
    .When(asker => asker?.Key.Type == typeof(IReportJob));
```

Collections, `Lazy<T>` and `Func<T>` are looked through: the condition sees the service that asked for the list, not
the list. `ResolveAll` and collections leave out registrations that decline.

See the [Conditional sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Conditional).

## Checking what is registered

```csharp
// While configuring: only registrations made so far.
if (!builder.IsRegistered(ServiceKey.For<IMailer>()))
    builder.Register<IMailer, SmtpMailer>();

// After Build: anything the container can supply.
bool known = scope.IsRegistered<IMailer>();
IMailer? mailer = scope.TryResolve<IMailer>();
```

`builder.IsRegistered` looks at the registrations made so far, open generics included. The scope's `IsRegistered`
also counts what the container makes up on the fly, so it is true for any collection type and for `Lazy<T>` of a
registered `T`.

`TryResolve` returns null when the service is not registered. A service that is registered but can't be built still
throws, so a broken dependency doesn't hide behind a null.

## Rules worth knowing

- The last registration of a service wins when you resolve one. A closed registration beats one made from an open
  generic.
- `ResolveAll` and collections return every registration in registration order.
- Keyed and unkeyed registrations are separate; one never answers for the other.
- The container does not dispose objects passed to `RegisterInstance`, or anything registered with `ExternallyOwned`.
- Only transient services accept arguments at resolve time.
- A builder can only be built once. Registering on it after `Build()` throws an `InvalidOperationException`.
