# API reference

Every public type and member of the Maxen.DI libraries, generated from the XML comments in the source.

## The core library (`Maxen.DI` package)

| Namespace | What is in it |
|---|---|
| <xref:Maxen.DI> | The container, the builder, scopes, service keys, lifetimes, attributes, exceptions and the everyday extension methods. |
| <xref:Maxen.DI.Pipeline> | The resolution pipeline: middleware, `ResolveContext` and middleware phases. |
| <xref:Maxen.DI.Diagnostics> | Validation, the dependency graph, resolution observers and telemetry. |
| <xref:Maxen.DI.Extensibility> | Modules, registration sources, constructor selectors, parameter resolvers and initializers. |
| <xref:Maxen.DI.Interception> | Interceptors and the invocation they see. |
| <xref:Maxen.DI.Async> | Async initialization: `IAsyncInitializable`, `ResolveAsync`, `InitializeSingletonsAsync`. |
| <xref:Maxen.DI.Arguments> | Values passed in at resolve time with `ResolveWith`. |
| <xref:Maxen.DI.Options> | `IOptions<T>`, `Configure<T>` and binding options from key/value pairs. |
| <xref:Maxen.DI.Scanning> | Registering the types in an assembly by convention. |

## Integration packages

| Namespace | Package | What is in it |
|---|---|---|
| <xref:Maxen.DI.Extensions.DependencyInjection> | `Maxen.DI.Extensions.DependencyInjection` | Maxen.DI behind `IServiceCollection` and the Generic Host. |
| <xref:Maxen.DI.Extensions.AspNetCore> | `Maxen.DI.Extensions.AspNetCore` | `UseMaxenDI` for web apps, request values and the diagnostics endpoint. |

New to the library? Start with [getting started](../docs/getting-started.md). The [guides](../docs/registering.md)
explain each feature with examples.
