# Getting started

## Install

```bash
dotnet add package Maxen.DI
dotnet add package Maxen.DI.Extensions.AspNetCore   # only for ASP.NET Core apps
```

The analyzers and the source generator come inside `Maxen.DI`, so there is nothing else to add for them.

## Register, build, resolve

```csharp
var builder = new ContainerBuilder();

builder.Register<IClock, SystemClock>().AsSingleton();
builder.Register<IOrderService, OrderService>().AsScoped();
builder.Register<IMailer, SmtpMailer>();                 // transient by default

await using var container = builder.Build();             // immutable from here on

using var scope = container.CreateScope();
var orders = scope.Resolve<IOrderService>();
```

The container uses the constructor with the most parameters it can satisfy. Services resolved from the root container
must be singletons or transients; ask for scoped ones from a scope.

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
builder.Register<IMailer, FakeMailer>().WhenInjectedInto<OrderTests>();   // only for one consumer
builder.Replace<IMailer, ConsoleMailer>();                                 // swap; fails if nothing to replace
builder.Override<IMailer, AuditedMailer>();                                // wins, keeps the original for ResolveAll
builder.Register<IMailer, SmtpMailer>().WithKey("primary");                // keyed; ask with [FromKey("primary")]
```

## Environments

```csharp
builder.DefineProfile("Development", p => p.Replace<IMailer, ConsoleMailer>());
builder.UseProfileFromEnvironment();      // reads MAXEN_PROFILE
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
- `builder.Options.ValidateOnBuild = true` checks the whole graph at startup: missing services, loops, captive dependencies.
- `builder.Observe(new ResolutionTrace())` records what was built and how long it took.
- While you type, the analyzers warn about a singleton that depends on a scoped service (`MAXEN004`, `MAXEN005`), a
  missing registration (`MAXEN006`) and more. [Diagnostics](diagnostics.md) lists them all.
- In CI, `maxen-di validate YourApp.dll` fails the build on any of these.

## Where next

- The [samples](https://github.com/pfimax/maxen.di/tree/main/samples) have a runnable example for every feature, one for the source
  generator, and ones for the Generic Host, ASP.NET Core and OpenTelemetry.
- [Extending](extending.md) shows how to write your own lifetime, middleware or registration source.
- [Migrating](migrating.md) maps Microsoft DI and Autofac calls onto Maxen.DI.
