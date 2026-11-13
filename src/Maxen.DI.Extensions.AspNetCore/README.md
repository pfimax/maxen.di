# Maxen.DI.Extensions.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Maxen.DI.Extensions.AspNetCore?label=NuGet&color=067a4f)](https://www.nuget.org/packages/Maxen.DI.Extensions.AspNetCore) ![.NET 8, 9, 10, 11](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010%20%7C%2011-512bd4) [![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/pfimax/maxen.di/blob/main/LICENSE)

Use [Maxen.DI](https://www.nuget.org/packages/Maxen.DI) as the container of an ASP.NET Core app in one line. Your
`builder.Services` registrations keep working, each request still gets its own scope, and you get a few web-specific
extras: request values you can inject anywhere, and a diagnostics endpoint that doubles as a readiness probe.

## Install

```bash
dotnet add package Maxen.DI.Extensions.AspNetCore
```

## Use it

```csharp
using Maxen.DI.Extensions.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGreeter, Greeter>();
builder.Services.AddScoped<WhoAmI>();

builder.UseMaxenDI(c =>
{
    c.Options.ValidateOnBuild = true;
    c.Decorate<IGreeter, ExcitedGreeter>();
});

var app = builder.Build();
```

The callback receives the Maxen.DI `ContainerBuilder` after everything in `builder.Services` has been copied into it,
so registrations there win, and decorators can wrap services added with `AddSingleton` and friends.

## Request values

Take the current request as an ordinary scoped service, instead of reaching for `IHttpContextAccessor`:

```csharp
builder.AddMaxenRequestValues();
builder.UseMaxenDI();

var app = builder.Build();
app.UseMaxenRequestValues();
```

```csharp
public sealed class WhoAmI(RequestValues request)
{
    public string Describe() =>
        $"{request.Request.Method} {request.Request.Path}";
}
```

`RequestValues` carries `HttpContext`, `Request`, `Response`, `User` and `TraceIdentifier`.

## Diagnostics endpoint

```csharp
app.MapMaxenDiagnostics().RequireAuthorization("Admins");
```

A GET endpoint (`/_maxen` by default) that answers with the registrations, the dependencies between them and any
validation problems, as JSON. It returns 200 when the container is sound and 503 when it isn't, so a load balancer or
Kubernetes readiness probe can point at it. The answer names your types, so keep it behind authorization.

## Good to know

- `httpContext.GetMaxenScope()` hands you the request's Maxen scope, for features the standard `IServiceProvider` has
  no method for, such as `ResolveWith`.
- Keyed services work, including `[FromKeyedServices]` on minimal API parameters, `[ServiceKey]` and
  `KeyedService.AnyKey`.
- When a service can't be built you get an `InvalidOperationException`, with Maxen's full explanation as its
  `InnerException`.
- Scope validation is on in every environment, not only in Development.

## Learn more

- [ASP.NET Core and the Generic Host](https://pfimax.github.io/maxen.di/docs/aspnetcore.html): the full guide
- [The WebApi sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.WebApi)
- [Documentation](https://pfimax.github.io/maxen.di/) · [Changelog](https://github.com/pfimax/maxen.di/blob/main/CHANGELOG.md)
