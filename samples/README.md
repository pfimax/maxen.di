# Samples

Every feature has a runnable example. Start with the feature tour; the other projects show what needs a host,
the source generator or a telemetry pipeline.

| Project | Run it | Shows |
|---|---|---|
| `Maxen.DI.Samples.Features` | `dotnet run -- all` or `dotnet run -- <name>` | one small example per feature (table below) |
| `Maxen.DI.Samples.Generators` | `dotnet run` | the source generator: `[Service]`, `[Decorator]` (open generics too), `[Intercept]` proxies, generated activators, and which analyzer warnings to expect |
| `Maxen.DI.Samples.Console` | `dotnet run` | a combined tour: profiles, decorators, scopes, tracing, telemetry; also the native AOT check |
| `Maxen.DI.Samples.WebApi` | `dotnet run` | ASP.NET Core: `UseMaxenDI`, `RequestValues`, feature-flagged module, `MapMaxenDiagnostics()` health endpoint |
| `Maxen.DI.Samples.GenericHost` | `dotnet run` | Generic Host and standalone `IServiceCollection` adapter, hosted services, decorating a Microsoft registration |
| `Maxen.DI.Samples.OpenTelemetry` | `dotnet run` | traces and metrics exported through OpenTelemetry using `MaxenTelemetry.Name` |

## Feature tour (`Maxen.DI.Samples.Features`)

| Name | Feature |
|---|---|
| `lifetimes` | transient, scoped, singleton, per-thread, pooled (`IPoolable`), tag-scoped (`AsScopedTo`) |
| `registration` | `Register`, `RegisterInstance`, `RegisterFactory`, `TryRegister`, `Replace`, `AlsoAs`, `ExternallyOwned` |
| `keyed` | `WithKey`, `Resolve<T>(key)`, `[FromKey]` |
| `generics` | open generic registrations and closed overrides |
| `collections` | `ResolveAll`, `IEnumerable<T>` injection |
| `deferred` | `Lazy<T>`, `Func<T>`, `Func<arg, T>`, `ResolveWith`, `WithRuntimeArguments` |
| `decorators` | `Decorate<TService, TDecorator>` |
| `interception` | `InterceptWith`, `Intercept<TService, TInterceptor>` |
| `conditional` | `WhenInjectedInto`, `When` |
| `injection` | property and method injection (`[Inject]`) |
| `profiles` | `DefineProfile`, `UseProfile` (and `UseProfileFromEnvironment`) |
| `modules` | `IContainerModule`, `RegisterModule`, feature flags (`UseFeatures`, `MAXEN_FEATURES`) |
| `scanning` | fluent `Scan`, `RegisterAttributed` |
| `asyncinit` | `IAsyncInitializable`, `ResolveAsync`, `InitializeSingletonsAsync` |
| `children` | `CreateChild` containers that inherit and override |
| `options` | `Configure<T>`, `BindOptions<T>`, `IOptions<T>` |
| `validation` | `Validate()`, `ValidateOnBuild`, captive dependency detection |
| `graph` | `BuildGraph()`, `ToMermaid()`, `ToText()` |
| `observability` | `Observe` + `ResolutionTrace`, `UseTelemetry` |
| `middleware` | global `Use(MiddlewarePhase, ...)`, per-registration `OnActivated` |
| `extensibility` | custom `Lifetime`, `IRegistrationSource`, `IParameterResolver`, `IInstanceInitializer` |
| `disposal` | scope-owned disposal order |

## The command line tool

`maxen-di` inspects the modules in a built assembly. The feature tour contains modules, so you can try the tool on it:

```bash
dotnet build samples/Maxen.DI.Samples.Features -c Release
maxen-di graph   samples/Maxen.DI.Samples.Features/bin/Release/net10.0/Maxen.DI.Samples.Features.dll --format mermaid
maxen-di explain samples/Maxen.DI.Samples.Features/bin/Release/net10.0/Maxen.DI.Samples.Features.dll IReportPrinter
maxen-di snapshot <dll> graph.json     # save a baseline, then later:
maxen-di diff <dll> --baseline graph.json
maxen-di init Billing --namespace Shop # write a starter module
```
