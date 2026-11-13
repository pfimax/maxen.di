# Getting started

## Install

```bash
dotnet add package Maxen.DI
# only for ASP.NET Core apps
dotnet add package Maxen.DI.Extensions.AspNetCore
```

The analyzers and the source generator come inside `Maxen.DI`, so there is nothing else to add for them.

## Register, build, resolve

```csharp
var builder = new ContainerBuilder();

builder.Register<IClock, SystemClock>().AsSingleton();
builder.Register<IOrderService, OrderService>().AsScoped();
// transient by default
builder.Register<IMailer, SmtpMailer>();

// the container can't be changed after this
await using var container = builder.Build();

using var scope = container.CreateScope();
var orders = scope.Resolve<IOrderService>();
```

The container uses the constructor with the most parameters it can satisfy. The root container hands out singletons,
transients and per-thread services; ask for scoped, pooled and named-scope services from a scope.

## Lifetimes

| Call | Meaning |
|---|---|
| `AsTransient()` | A new object every time (the default). |
| `AsScoped()` | One per scope. |
| `AsSingleton()` | One per container. |
| `AsScopedTo("request")` | One per scope *labelled* `"request"`, shared by scopes nested inside it. |
| `AsPooled(max)` | One per scope, kept for reuse by later scopes. |
| `AsPerThread()` | One per thread. |

## Choosing between registrations

```csharp
builder.Register<IMailer, SmtpMailer>();

// only for one consumer
builder.Register<IMailer, FakeMailer>()
    .WhenInjectedInto<OrderTests>();

// swap; fails if there was nothing to replace
builder.Replace<IMailer, ConsoleMailer>();

// wins, but the original still shows up in ResolveAll
builder.Override<IMailer, AuditedMailer>();

// keyed; ask for it with [FromKey("primary")]
builder.Register<IMailer, SmtpMailer>().WithKey("primary");
```

[Registering services](registering.md) covers all of these in more detail.

## Keyed services

A key picks one of several registrations of the same service. Ask for it with `scope.Resolve<IMailer>("primary")`, or
mark a constructor parameter with `[FromKey("primary")]`. Microsoft's `[FromKeyedServices("primary")]` works the same.

When you can't list the keys up front, register once for all of them:

```csharp
builder.Register<ITenantDb, TenantDb>()
    .WithKey(ServiceKey.AnyKey)
    .AsSingleton();

public class TenantDb : ITenantDb
{
    // tenant is the key this instance was asked for with
    public TenantDb([ServiceKey] string tenant) { }
}

// its own singleton, built with tenant = "acme"
var acme = scope.Resolve<ITenantDb>("acme");
```

`[ServiceKey]` comes from `Microsoft.Extensions.DependencyInjection.Abstractions`. A key registered explicitly, such as
`.WithKey("acme")`, always wins over `AnyKey`.

## Environments

```csharp
builder.DefineProfile("Development",
    p => p.Replace<IMailer, ConsoleMailer>());

// reads the MAXEN_PROFILE environment variable
builder.UseProfileFromEnvironment();
```

## ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGreeter, Greeter>();
builder.UseMaxenDI(c => c.Decorate<IGreeter, ExcitedGreeter>());
```

Your existing `AddSingleton`, `AddScoped` and `AddTransient` calls keep working. The callback is where the Maxen-only
features go.

## When something goes wrong

- `ServiceNotRegisteredException` lists likely matches ("did you mean ...").
- `builder.Options.ValidateOnBuild = true` checks the whole graph at startup: missing services, loops, captive
  dependencies. Decorators and `[Service]` classes are checked too.
- `var trace = new ResolutionTrace(); builder.Observe(trace);` records what was built and how long it took.
- While you type, the analyzers warn about a singleton that depends on a scoped service (`MAXEN004`, `MAXEN005`), a
  missing registration (`MAXEN006`), a scoped service resolved from the root container (`MAXEN010`) and more.
  [Diagnostics](diagnostics.md) lists them all.
- A scope that has been disposed refuses every request with an `ObjectDisposedException`, so a service used after
  its scope ended fails where the mistake is.
- In CI, `maxen-di validate YourApp.dll` fails the build on any of these. It reads the registrations in the
  `IContainerModule` classes of your assembly.

## Where next

- [Registering services](registering.md) and [lifetimes, scopes and disposal](lifetimes.md) cover the everyday API.
- [Validation and diagnostics](validation-and-diagnostics.md) shows every way to catch a wiring mistake.
- [ASP.NET Core and the Generic Host](aspnetcore.md) explains what changes when Maxen.DI runs behind
  `IServiceCollection`.
- [Moving from another container](migrating.md) maps Microsoft DI and Autofac calls onto Maxen.DI.
- The [samples](https://github.com/pfimax/maxen.di/tree/main/samples) have a runnable example for every feature.
