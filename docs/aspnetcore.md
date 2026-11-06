# ASP.NET Core and the Generic Host

You don't have to give up `IServiceCollection` to use Maxen.DI. The adapter packages copy everything registered with
`AddSingleton`, `AddScoped` and `AddTransient` into a Maxen container and hand the framework a provider that behaves
the way it expects. This page shows how to plug it in, what gets translated and how, and the extras you get in a web
app: request values, a diagnostics endpoint and resolution logging.

## Why run it behind IServiceCollection

ASP.NET Core, the Generic Host and most libraries register their services through `IServiceCollection`. Running
Maxen.DI behind it means all of that keeps working unchanged, and you get the Maxen features on top: decorators,
interceptors, profiles, validation, observers and the rest. It is also the safest way to move an existing app over,
because you can swap the container first and touch registrations later. [Migrating](migrating.md) has the call-by-call
mapping.

```bash
# Generic Host, worker services, console apps
dotnet add package Maxen.DI.Extensions.DependencyInjection

# ASP.NET Core (brings the package above with it)
dotnet add package Maxen.DI.Extensions.AspNetCore
```

## Plugging it in

### ASP.NET Core

```csharp
using Maxen.DI.Extensions.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGreeter, Greeter>();
builder.Services.AddScoped<WhoAmI>();

builder.UseMaxenDI(c => c.Decorate<IGreeter, ExcitedGreeter>());

var app = builder.Build();
```

`UseMaxenDI` comes from `Maxen.DI.Extensions.AspNetCore` and returns the same `WebApplicationBuilder`, so it chains.
Where you call it among your `builder.Services` calls doesn't matter: the collection is copied when the app is built.

### Generic Host

```csharp
using Maxen.DI.Extensions.DependencyInjection;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddHostedService<HeartbeatService>();
    })
    .UseMaxenDI(c => c.Decorate<IClock, FixedClock>())
    .Build();
```

Hosted services, logging and configuration keep working; the hosted services are built by Maxen.DI with their
dependencies.

The extension is for `IHostBuilder`. With `Host.CreateApplicationBuilder`, pass the factory class the extension uses
to the builder's `ConfigureContainer` method instead:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.ConfigureContainer(new MaxenServiceProviderFactory(
    c => c.Decorate<IClock, FixedClock>()));
```

### Without a host

```csharp
var services = new ServiceCollection();
services.AddSingleton<IClock, SystemClock>();

await using var provider = (MaxenServiceProvider)
    services.BuildMaxenServiceProvider(
        c => c.Options.ValidateOnBuild = true);
```

`BuildMaxenServiceProvider` returns an `IServiceProvider`. Dispose it when you are done: that disposes the container
and the singletons it created. The concrete type, `MaxenServiceProvider`, exposes the Maxen scope behind it as
`Scope`.

If you would rather build the container yourself, copy the collection into your own builder with `Populate`:

```csharp
var builder = new ContainerBuilder();
builder.Populate(services);
builder.Register<IReportJob, ReportJob>();

await using var container = builder.Build();
```

All of these go through `MaxenServiceProviderFactory`, an `IServiceProviderFactory<ContainerBuilder>`, which you can
also hand to any API that accepts one.

## What the callback is for

The callback receives the <xref:Maxen.DI.ContainerBuilder> after everything in the service collection has been
copied into it. Put the Maxen-only parts there: decorators, interceptors, profiles and features, modules, observers,
and `builder.Options`.

Because it runs last, a registration made in the callback wins over one from the collection (the last registration
of a service wins), and `Decorate` can wrap a service that was added with `AddSingleton`.

```csharp
builder.UseMaxenDI(c =>
{
    c.Options.ValidateOnBuild = true;
    c.Decorate<IGreeter, ExcitedGreeter>();
    c.RegisterModule("quiet", new QuietGreetingModule());
    c.UseFeaturesFromEnvironment();
});
```

## How registrations are translated

| In the collection | In Maxen.DI |
|---|---|
| `AddSingleton`, `AddScoped`, `AddTransient` | The same lifetime. |
| An implementation type | A type registration: Maxen.DI picks the constructor, and validation can read it. |
| A factory `sp => ...` | A factory registration. The `sp` it receives is the scope doing the resolving. |
| An instance | An instance registration. The container does not dispose it, as with Microsoft's container. |
| Open generics | Open generic registrations. |
| `AddKeyed...` | The same registration with its key; `KeyedService.AnyKey` becomes `ServiceKey.AnyKey`. |

The order is kept, so the last registration of a service is the one you get and `IEnumerable<T>` returns all of them
in registration order, the same as Microsoft's container. `TryAdd...` calls are decided by the collection before
Maxen.DI sees it.

Factories are opaque to validation, so a service registered as `sp => new Foo(...)` won't show its dependencies in
the graph. Prefer type registrations where you can, or switch on `ValidateFactories`; see
[validation and diagnostics](validation-and-diagnostics.md).

## What is supported

- **Keyed services.** `AddKeyedSingleton` and friends, `GetKeyedService`, `GetRequiredKeyedService`,
  `[FromKeyedServices("key")]` on constructor and minimal API parameters, `[FromKeyedServices]` without a key (the
  key of the service being built), `[ServiceKey]`, and `KeyedService.AnyKey`. A keyed factory receives the key it was
  asked with, also for an any-key registration.
- **`IServiceScopeFactory`**, so ASP.NET Core and hosted services can create scopes.
- **`IServiceProviderIsService`** and **`IServiceProviderIsKeyedService`**, which minimal APIs use to decide which
  parameters come from the container.
- **`IServiceProvider`** itself: asking for it gives you the provider for the current scope.

The adapter passes Microsoft's own conformance suite (`Maxen.DI.Specification.Tests`), keyed services included: 143
tests on .NET 10, and 139 on .NET 8 and 9 because four keyed tests only exist on .NET 10. The suite runs with scope
validation switched off, for the reason given below. [Comparison](comparison.md) has the details.

## One scope per request

ASP.NET Core asks `IServiceScopeFactory` for a scope at the start of each request, so each request gets its own Maxen
scope, and middleware, filters and the endpoint share its scoped services. `HttpContext.RequestServices` is a
`MaxenServiceProvider` around that scope.

For Maxen features the standard `IServiceProvider` has no method for, get the scope itself:

```csharp
app.MapGet("/report", (HttpContext http) =>
{
    var scope = http.GetMaxenScope()!;
    return scope.ResolveWith<Report>(new { title = "Q3" }).Render();
});
```

`GetMaxenScope()` returns the request's `IScope`, or null when the app isn't running on Maxen.DI.

## Request values

`RequestValues` is the current request as an ordinary scoped service. Take it in a constructor like any other
dependency, instead of reaching for `IHttpContextAccessor`:

```csharp
builder.AddMaxenRequestValues();
builder.UseMaxenDI();

var app = builder.Build();
app.UseMaxenRequestValues();
```

```csharp
public sealed class WhoAmI
{
    private readonly RequestValues _request;

    public WhoAmI(RequestValues request) => _request = request;

    public string Describe() =>
        $"{_request.Request.Method} {_request.Request.Path}";
}
```

It carries `HttpContext`, `Request`, `Response`, `User` and `TraceIdentifier`, plus `IsAttached`.

`AddMaxenRequestValues` registers it as a scoped service. `UseMaxenRequestValues` adds middleware that fills it in at
the start of each request, so put it before anything that needs the values. If the middleware is missing, or the
object is used outside a request, reading any of its properties throws an `InvalidOperationException` that tells you
to call `app.UseMaxenRequestValues()`. Code that can also run outside a request can check `IsAttached` first.

Being scoped, it can't be taken by a singleton; scope validation will stop you.

## The diagnostics endpoint

```csharp
app.MapMaxenDiagnostics();               // GET /_maxen
app.MapMaxenDiagnostics("/health/di");   // or a route of your own
```

This maps a GET endpoint that answers with what the container holds and whether validation passes. Shortened, the
answer looks like this:

```json
{
  "healthy": true,
  "registrations": [
    {
      "service": "IClock",
      "lifetime": "Singleton",
      "implementation": "IClock -> Clock (Singleton)"
    }
  ],
  "dependencies": [
    { "from": "IGreeter", "to": "IClock", "deferred": false }
  ],
  "issues": []
}
```

Each issue has `kind` (a `ValidationIssueKind` such as `MissingDependency`), `service` and `message`. The status is
200 when there are no issues and 503 when there are, so a load balancer or Kubernetes readiness probe can point at
it. If the app isn't running on Maxen.DI, it answers 501.

A few things to know before you rely on it:

- It builds the graph and runs `Validate()` once, on the first request, and answers every later request from that
  result. The configuration can't change after `Build()`, so the answer can't either; it tells you whether this
  deployment is sound. With `ValidateFactories` on, your factories therefore run once, not on every probe.
- The JSON names your types. `MapMaxenDiagnostics` returns a `RouteHandlerBuilder`, so you can put it behind
  authorization, or limit it to an internal host name if your probe can't authenticate:

```csharp
app.MapMaxenDiagnostics().RequireAuthorization("Admins");
app.MapMaxenDiagnostics("/ready").RequireHost("*:8081");
```

## Logging resolutions

`LogResolutions` sends every resolution to an `ILogger`: successes at the level you choose (Debug by default),
failures as errors with the exception, and anything at or over the slow threshold as a warning.

```csharp
using var loggerFactory = LoggerFactory.Create(l => l.AddConsole());
var logger = loggerFactory.CreateLogger("Maxen.DI");

builder.UseMaxenDI(c => c.LogResolutions(
    logger,
    LogLevel.Debug,
    slowThreshold: TimeSpan.FromMilliseconds(5)));
```

The callback runs while the container is being built, before the app's own logger factory exists, which is why the
example creates a logger of its own. Like any observer, this sees cached hits too, so singletons lose their fast path
while it is on. [Validation and diagnostics](validation-and-diagnostics.md#watching-resolutions) covers observers and
telemetry in general.

## Binding options from configuration

`BindOptions` binds a configuration section (appsettings.json, environment variables and the rest) onto a settings
class:

```csharp
builder.UseMaxenDI(c =>
    c.BindOptions<DatabaseSettings>(
        builder.Configuration.GetSection("Database")));
```

With the Generic Host, the configuration reaches you through `ConfigureContainer`:

```csharp
Host.CreateDefaultBuilder(args)
    .UseMaxenDI()
    .ConfigureContainer<ContainerBuilder>((context, c) =>
        c.BindOptions<DatabaseSettings>(
            context.Configuration.GetSection("Database")));
```

Inject the result as `IOptions<DatabaseSettings>` from `Maxen.DI.Options`. That is Maxen's own options interface,
not the one from `Microsoft.Extensions.Options`; `services.Configure<T>(section)` still fills Microsoft's as usual.
Binding happens when the options are first read, and a later `c.Configure<DatabaseSettings>(...)` step can still
adjust what the file said. It uses reflection, so it is marked as not trim or native AOT safe.
[Configuration](configuration.md) covers options in general.

## When something can't be built

Code written for Microsoft's container, ASP.NET Core included, expects an `InvalidOperationException` when a service
can't be built, and some of it catches exactly that type. So the provider wraps Maxen's exceptions:

- `GetService` returns null for a service that isn't registered.
- `GetRequiredService` throws `InvalidOperationException` with Microsoft's wording ("No service for type ... has been
  registered.") when the service you asked for isn't registered.
- Any other failure is an `InvalidOperationException` with Maxen's message, which names the service and the
  dependency path.

In every case the `InnerException` is the original Maxen exception, such as `ServiceNotRegisteredException` with
its suggestions. If `ValidateOnBuild` is on and finds problems, the `ContainerValidationException` is thrown when the
host or app is built, before any request.

## Scope validation is on

Maxen.DI validates scopes in every environment. Resolving a scoped service from the root provider, or building a
singleton that needs a scoped one, throws, with the full dependency path. Microsoft's container only checks this when
asked to, and the host asks only in the Development environment. Settings made with `UseDefaultServiceProvider` don't
apply once Maxen.DI is the container; use `builder.Options` in the callback instead.

A common place to hit this is a hosted service, which is a singleton, taking a scoped service in its constructor.
Take `IServiceScopeFactory` instead and create a scope for each unit of work.

If a library you depend on resolves scoped services from the root and you can't change it, switch the check off:

```csharp
builder.UseMaxenDI(c => c.Options.ValidateScopes = false);
```

That is how the conformance suite runs, because some of its tests do exactly that. With it off, a scoped service
resolved from the root behaves like a singleton for the life of the app.

## Samples

- [WebApi](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.WebApi): `UseMaxenDI` with a
  decorator and a feature module, request values, `GetMaxenScope()` and the diagnostics endpoint.
- [GenericHost](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.GenericHost): a hosted service
  built by Maxen.DI, a decorator on a Microsoft registration, and `BuildMaxenServiceProvider` without a host.
