# Maxen.DI.Extensions.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/Maxen.DI.Extensions.DependencyInjection?label=NuGet&color=067a4f)](https://www.nuget.org/packages/Maxen.DI.Extensions.DependencyInjection) ![.NET 8, 9, 10, 11](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010%20%7C%2011-512bd4) [![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/pfimax/maxen.di/blob/main/LICENSE)

Run [Maxen.DI](https://www.nuget.org/packages/Maxen.DI) as the container behind `Microsoft.Extensions.DependencyInjection`
and the Generic Host. Everything you register with `AddSingleton`, `AddScoped` and `AddTransient` keeps working, and you
get Maxen.DI's validation, decorators, interceptors and profiles on top.

For ASP.NET Core, install [Maxen.DI.Extensions.AspNetCore](https://www.nuget.org/packages/Maxen.DI.Extensions.AspNetCore)
instead; it brings this package with it.

## Install

```bash
dotnet add package Maxen.DI.Extensions.DependencyInjection
```

## Generic Host

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

The callback receives the Maxen.DI `ContainerBuilder` after the service collection has been copied into it. That is
where the Maxen-only features go: decorators, interceptors, profiles, modules and `c.Options`.

With `Host.CreateApplicationBuilder`, pass the factory to `ConfigureContainer` instead:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.ConfigureContainer(new MaxenServiceProviderFactory(
    c => c.Decorate<IClock, FixedClock>()));
```

## Without a host

```csharp
var services = new ServiceCollection();
services.AddSingleton<IClock, SystemClock>();

await using var provider = (MaxenServiceProvider)
    services.BuildMaxenServiceProvider(
        c => c.Options.ValidateOnBuild = true);
```

## Good to know

- **Keyed services** work, including `[FromKeyedServices]`, `[ServiceKey]` and `KeyedService.AnyKey`, along with
  `IServiceScopeFactory`, `IServiceProviderIsService` and `IServiceProviderIsKeyedService`. The adapter passes
  Microsoft's own conformance tests.
- **Errors** come out as the `InvalidOperationException` that code written for Microsoft's container expects. The
  `InnerException` is Maxen's own exception, with the dependency path and "did you mean" suggestions.
- **Scope validation is on in every environment.** Resolving a scoped service from the root, or a singleton that takes
  a scoped one, throws. Turn it off with `c.Options.ValidateScopes = false` if a library you can't change relies on it.
- **Options from configuration:** `c.BindOptions<T>(configuration.GetSection("Db"))` fills Maxen's own
  `Maxen.DI.Options.IOptions<T>`, not Microsoft's.
- **Logging:** `c.LogResolutions(logger)` sends every resolution to an `ILogger`, with slow ones as warnings.

## Learn more

- [ASP.NET Core and the Generic Host](https://pfimax.github.io/maxen.di/docs/aspnetcore.html): the full guide
- [Moving from another container](https://pfimax.github.io/maxen.di/docs/migrating.html)
- [Documentation](https://pfimax.github.io/maxen.di/) · [Samples](https://github.com/pfimax/maxen.di/tree/main/samples) · [Changelog](https://github.com/pfimax/maxen.di/blob/main/CHANGELOG.md)
