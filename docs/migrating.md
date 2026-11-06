# Moving to Maxen.DI

This page maps the calls you already know from `Microsoft.Extensions.DependencyInjection` and Autofac onto Maxen.DI.
You do not have to move everything at once: `Maxen.DI.Extensions.DependencyInjection` lets Maxen.DI run behind an
existing `IServiceCollection`, so you can keep your `AddXxx` calls and switch the container first.

## Stay on `IServiceCollection`, swap the container

```csharp
// Generic Host
Host.CreateDefaultBuilder()
    .UseMaxenDI(b => b.Decorate<IGreeter, LoudGreeter>());

// ASP.NET Core (Maxen.DI.Extensions.AspNetCore)
WebApplication.CreateBuilder(args).UseMaxenDI();

// Or build a provider yourself
var provider = services.BuildMaxenServiceProvider();
```

Everything registered with `AddSingleton`, `AddScoped` and `AddTransient` keeps its meaning. Do this first and run your
tests; it shows whether anything depended on container-specific behaviour before you touch a single registration.

The adapter passes Microsoft's own conformance suite, keyed services included (`Maxen.DI.Specification.Tests`,
143 tests on .NET 10, 139 on .NET 8 and 9). One setting differs from what that suite assumes: Microsoft's provider
only validates scopes when asked (the host asks in the Development environment), while Maxen.DI always does unless you
turn it off, so the suite runs with `builder.Options.ValidateScopes = false`.

## Microsoft.Extensions.DependencyInjection

| Microsoft | Maxen.DI |
|---|---|
| `services.AddSingleton<IFoo, Foo>()` | `builder.Register<IFoo, Foo>().AsSingleton()` |
| `services.AddScoped<IFoo, Foo>()` | `builder.Register<IFoo, Foo>().AsScoped()` |
| `services.AddTransient<IFoo, Foo>()` | `builder.Register<IFoo, Foo>()` (transient is the default) |
| `services.AddSingleton<IFoo>(instance)` | `builder.RegisterInstance<IFoo>(instance)` |
| `services.AddScoped<IFoo>(sp => ...)` | `builder.RegisterFactory<IFoo>(ctx => ...).AsScoped()` |
| `services.AddKeyedSingleton<IFoo, Foo>("a")` | `builder.Register<IFoo, Foo>().AsSingleton().WithKey("a")` |
| `[FromKeyedServices("a")]`, `[FromKeyedServices]`, `[ServiceKey]` | work as they are; `[FromKey("a")]` is Maxen's own spelling |
| `services.AddKeyedSingleton<IFoo, Foo>(KeyedService.AnyKey)` | `builder.Register<IFoo, Foo>().AsSingleton().WithKey(ServiceKey.AnyKey)` |
| `services.AddSingleton(typeof(IRepo<>), typeof(Repo<>))` | `builder.Register(typeof(IRepo<>), typeof(Repo<>)).AsSingleton()` |
| `services.Replace(...)` | `builder.Replace<IFoo, Foo>()` (or `Override` to keep the original in `ResolveAll`) |
| `services.TryAddSingleton<IFoo, Foo>()` | `builder.TryRegister<IFoo, Foo>()?.AsSingleton()` (returns null when `IFoo` is already registered) |
| `provider.CreateScope()` | `container.CreateScope()` |
| `provider.GetRequiredService<T>()` | `scope.Resolve<T>()` |
| `provider.GetService<T>()` | `scope.TryResolve<T>()` |
| `provider.GetServices<T>()` | `scope.ResolveAll<T>()` |
| `ValidateScopes` / `ValidateOnBuild` | `builder.Options.ValidateScopes` / `builder.Options.ValidateOnBuild` |

Differences worth knowing:

- **Scope validation is on by default.** Resolving a scoped service from the root container throws, and a singleton that
  takes a scoped service is rejected, with the full dependency path in the message.
- **The last registration wins**, same as Microsoft. `ResolveAll` returns every registration in registration order.
- **Errors.** When a service can't be built, `GetService` and `GetRequiredService` throw `InvalidOperationException`, as
  Microsoft's container does. The `InnerException` is Maxen's own exception, with the dependency path and suggestions.
- **Disposal.** The scope that built a transient disposes it. The root container also tracks transients, so resolving a
  disposable transient from the root keeps it alive until the container is disposed. Analyzer `MAXEN007` warns about that.

## Autofac

| Autofac | Maxen.DI |
|---|---|
| `builder.RegisterType<Foo>().As<IFoo>()` | `builder.Register<IFoo, Foo>()` |
| `.SingleInstance()` | `.AsSingleton()` |
| `.InstancePerLifetimeScope()` | `.AsScoped()` |
| `.InstancePerDependency()` | `.AsTransient()` (the default) |
| `.Keyed<IFoo>("a")` / `.Named<IFoo>("a")` | `.WithKey("a")` |
| `.AsSelf().As<IFoo>()` | `.AlsoAs<IFoo>()` |
| `builder.RegisterInstance(x)` | `builder.RegisterInstance(x)` |
| `builder.Register(c => new Foo(c.Resolve<IBar>()))` | `builder.RegisterFactory<Foo>(ctx => new Foo(ctx.Resolve<IBar>()))` |
| `builder.RegisterGeneric(typeof(Repo<>)).As(typeof(IRepo<>))` | `builder.Register(typeof(IRepo<>), typeof(Repo<>))` |
| `builder.RegisterDecorator<LoudGreeter, IGreeter>()` | `builder.Decorate<IGreeter, LoudGreeter>()` |
| `.OnActivated(e => ...)` | `.OnActivated((instance, ctx) => ...)` |
| `Module` | `IContainerModule` plus `builder.RegisterModule(...)` |
| `builder.RegisterAssemblyTypes(a).AsImplementedInterfaces()` | `builder.Scan(a, s => s.AsImplementedInterfaces())` |
| `container.BeginLifetimeScope()` | `container.CreateScope()` |
| `scope.Resolve<T>()` | `scope.Resolve<T>()` |
| `scope.ResolveKeyed<T>("a")` | `scope.Resolve<T>("a")` |
| `IEnumerable<T>`, `Lazy<T>`, `Func<T>` | Same: all three are built in |

Differences worth knowing:

- Registrations are immutable once `Build()` returns; there is no `ContainerBuilder.Update`. Use `container.CreateChild(...)`
  when a part of the application needs its own overrides (tests, tenants, plugins).
- Decorators are matched by service type and ordered with a priority; lower numbers sit closer to the real service.
- Property and method injection are opt-in (`UsePropertyInjection`, `UseMethodInjection`), constructor injection is the default.

## Checking the move

1. Set `builder.Options.ValidateOnBuild = true` to catch missing dependencies, loops and lifetime mismatches at startup
   instead of at the first request. The `maxen-di` tool can run the same checks in CI, but it only sees registrations
   made in `IContainerModule` classes, so move them into a module first if you want that.
2. Run `maxen-di graph YourApp.dll --format mermaid` before and after, and `maxen-di diff old.dll new.dll` to see exactly
   which registrations or dependencies changed.
3. Add the analyzers (they ship in the `Maxen.DI` package): `MAXEN005` captive dependencies, `MAXEN006` missing
   registrations, `MAXEN007` disposable transients resolved from the root.
