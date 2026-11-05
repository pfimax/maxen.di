# Maxen.DI

**A dependency injection container that tells you what is wrong before your users do.**

Most containers wait until a request is half-way through to say "I couldn't build that". Maxen.DI checks your wiring while you
type, again when you build, and once more in CI, and then gets out of the way and resolves quickly. It is open enough to
change almost everything about how it behaves.

- **Catch mistakes early.** A missing registration, a loop, a singleton holding on to a per-request service: found by analyzers
  as you write the code, by `Validate()` at startup, and by a command line tool in your pipeline. Many come with a one-click fix.
- **Extensible all the way down.** Lifetimes, resolution middleware, registration sources, parameter resolvers,
  initializers, interceptors and observers are all plain interfaces you can implement. The built-in features use the same ones.
- **Reflection when you want it, none when you don't.** A source generator writes registrations, decorators, interceptor proxies
  and constructor calls at compile time, so your registrations stay friendly to trimming and native AOT.
- **A good citizen.** Run it behind `Microsoft.Extensions.DependencyInjection`, the Generic Host and ASP.NET Core. It passes
  Microsoft's own conformance suite, so the rest of your stack keeps working.
- **Honest about speed.** Singletons and repeat scoped lookups cost tens of nanoseconds; there is a
  [comparison page](docs/comparison.md) with numbers, including the places where Microsoft's container is faster.

Targets `net8.0`, `net9.0` and `net10.0`. Root namespace: `Maxen.DI`.

## A first look

```csharp
var builder = new ContainerBuilder();
builder.Register<IClock, SystemClock>().AsSingleton();
builder.Register<IGreeter, Greeter>().AsScoped();
builder.Register(typeof(IRepo<>), typeof(Repo<>));          // open generics
builder.Decorate<IGreeter, LoudGreeter>();                   // wrap a service without touching it

await using var container = builder.Build();
await using var scope = container.CreateScope();
var greeter = scope.Resolve<IGreeter>();
```

Prefer attributes? Mark the class and let the generator do the wiring at compile time:

```csharp
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IClock))]
public class SystemClock : IClock { }

builder.RegisterGeneratedServices();   // no reflection at startup
```

## What goes wrong, and how Maxen.DI helps

| Mistake | Where you hear about it |
|---|---|
| A constructor needs a service nobody registered | `MAXEN006` while typing (with a fix), `container.Validate()`, `maxen-di validate` |
| A singleton quietly captures a per-request service | `MAXEN004`/`MAXEN005` while typing (with a fix), `ValidateScopes` at run time |
| A disposable transient resolved from the root container leaks | `MAXEN007` while typing (with a fix) |
| `Lazy<T>` / `Func<T>` of a scoped service inside a singleton | `MAXEN009` while typing |
| A decorator that cannot wrap what it decorates | `MAXEN008` while typing |
| A pull request changes the dependency graph by accident | `maxen-di diff` fails the build |
| "Why did it pick that implementation?" | `maxen-di explain`, `ResolutionTrace`, helpful "did you mean" errors |

Every diagnostic is described, with the fix, in [docs/diagnostics.md](docs/diagnostics.md).

## Install

| Package | Use it for |
|---|---|
| `Maxen.DI` | The framework. The source generator, analyzers and code fixes ship inside it. |
| `Maxen.DI.Extensions.DependencyInjection` | Maxen.DI behind `Microsoft.Extensions.DependencyInjection` and the Generic Host. |
| `Maxen.DI.Extensions.AspNetCore` | `WebApplication.CreateBuilder().UseMaxenDI()`, request values, a diagnostics endpoint. |
| `Maxen.DI.Tool` | The `maxen-di` command line tool (`dotnet tool install -g Maxen.DI.Tool`). |

## Where to go next

- **See it work:** [samples/](samples/README.md) has a runnable example for every feature, one for the source generator,
  one each for the Generic Host, ASP.NET Core and OpenTelemetry.
- **Learn it:** [getting started](docs/getting-started.md), [extending](docs/extending.md),
  [migrating](docs/migrating.md) from Microsoft DI or Autofac.
- **Check it:** [diagnostics reference](docs/diagnostics.md) and the [comparison with Microsoft DI and Autofac](docs/comparison.md).

Repository layout: `src/` (libraries, generator, code fixes, tool), `tests/`, `benchmarks/`, `samples/`, `docs/`, `scripts/`.

---

# Feature guide

## The basics

Transient, scoped and singleton lifetimes, keyed services (`.WithKey` and `[FromKey]`), open generics, decorators,
`AlsoAs` (one instance under several service types), `IEnumerable<T>` / `T[]` / `Lazy<T>` / `Func<T>`, sync and async
disposal, circular-dependency detection, scope validation, thread safety, and attribute and convention scanning.

## Catching problems

### Validation at startup
```csharp
builder.Options.ValidateOnBuild = true;   // Build() throws one exception listing every problem
var issues = container.Validate();        // or ask yourself: missing dependencies, loops, captive dependencies
```
Validation reads constructors only, so services built by factories are opaque to it. Services that take values at
resolve time should be marked `.WithRuntimeArguments()`.

Two more checks are built in. A `WhenInjectedInto<X>` registration where nothing registered is an `X` is reported as
`UnreachableRegistration`. And with `builder.Options.ValidateFactories = true`, validation also *runs* each factory
registration once (from a throwaway scope) and reports the ones that throw as `FactoryFailed`. Factories run for real,
so leave it off if they have side effects.

### Diagnostics
```csharp
var trace = new ResolutionTrace();
builder.Observe(trace);                                       // tree of what was built, with timings
builder.ObserveSlowResolutions(TimeSpan.FromMilliseconds(5), e => log(e));
File.WriteAllText("graph.md", container.BuildGraph().ToMermaid());   // also ToDot() and ToText()
builder.UseTelemetry();                                       // activities and metrics under the name "Maxen.DI"
```
`MaxenTelemetry.Name` is the one name OpenTelemetry needs; see `samples/Maxen.DI.Samples.OpenTelemetry`.

### Helpful errors
A missing service says what you probably meant: the keyed registration you forgot the key for, the interface a
class was registered under, or a near-miss name. They are also available as `ServiceNotRegisteredException.Suggestions`.

## Shaping behaviour

### Decorators and interceptors
```csharp
builder.Decorate<IGreeter, LoudGreeter>(priority: 0);      // lower priority sits closer to the service
builder.Decorate<IGreeter, ExcitedGreeter>(priority: 10);  // equal priorities keep registration order

builder.Register<IOrders, Orders>().InterceptWith<LoggingInterceptor>();
builder.Intercept<IOrders, TimingInterceptor>();           // or every registration of a service
```
Interceptors implement `IInterceptor`; call `invocation.Proceed()` to let the call through. Interface services only.

### Replace, override and conditional registrations
```csharp
builder.Replace<IMailer, ConsoleMailer>();            // removes earlier ones; throws if there was nothing to replace
builder.Override<IMailer, AuditedMailer>();           // wins, but the original still shows up in ResolveAll
builder.Register<IClock, FrozenClock>().WhenInjectedInto<ReportJob>();   // or .When(asker => ...)
```
A conditional registration that declines the asker falls back to the next most recent one.

### Profiles and feature flags
```csharp
builder.DefineProfile("Development", p => p.Register<IMailer, FakeMailer>());
builder.UseProfile("Development");        // or builder.UseProfileFromEnvironment() to read MAXEN_PROFILE

builder.RegisterModule<BetaModule>("beta");   // only applied when the "beta" feature is on
builder.UseFeaturesFromEnvironment();         // MAXEN_FEATURES=beta,other
```
Profiles are applied at `Build()`, after everything else, so they override the defaults. Features are profiles under
another name. Switching on a profile that was never defined fails with a list of the ones that were.

### Options
```csharp
builder.Configure<DatabaseSettings>(s => s.Host = "db");
builder.BindOptions<DatabaseSettings>(values, section: "Database");   // flat "Database:Port" style pairs
// inject IOptions<DatabaseSettings>
```

### Fluent scanning
```csharp
builder.Scan(assembly, s => s.InNamespace("MyApp.Services").AsImplementedInterfaces().AsSingleton());
```

### Child containers
`container.CreateChild(b => b.Register<IMail, FakeMail>())` falls back to the parent for anything it does not
register, shares the parent's singletons and inherits its middleware, decorators and other extensions.

### Arguments at resolve time and auto-factories
```csharp
scope.ResolveWith<Report>(new { title = "Q3" });             // by parameter name, transient services only
var make = scope.Resolve<Func<string, int, Report>>();       // by type and position, up to four arguments
```

### Async initialization and more lifetimes
`IAsyncInitializable` + `await scope.ResolveAsync<T>()` (runs once per instance) and
`await container.InitializeSingletonsAsync()` (dependencies first). `.AsPooled(max)` keeps instances for reuse by later
scopes (`IPoolable.Reset()` is called in between); `.AsPerThread()` gives one instance per thread.

### Named scopes
```csharp
builder.Register<RequestContext>().AsScopedTo("request");
using var request = container.CreateScope("request");
using var step = request.CreateScope();               // shares the request's instance
```
Disposing a parent scope does not dispose scopes created from it; each owner disposes its own. If you would rather
the parent cleaned up after its children, set `builder.Options.DisposeChildScopes = true`.

### Method and property injection
`builder.UseMethodInjection()` calls public methods marked `[Inject]` after construction, like `UsePropertyInjection()`
does for properties. Both are off by default; constructors stay the main way in.

## Working with the rest of .NET

### Microsoft.Extensions.DependencyInjection, Generic Host and ASP.NET Core
```csharp
Host.CreateDefaultBuilder().UseMaxenDI(b => b.Decorate<IGreeter, LoudGreeter>());
WebApplication.CreateBuilder(args).UseMaxenDI();           // Maxen.DI.Extensions.AspNetCore
// or: var provider = services.BuildMaxenServiceProvider();
```
Settings from `appsettings.json` bind with `builder.BindOptions<Db>(configuration.GetSection("Db"))`. Keyed services,
`IServiceScopeFactory` and `IServiceProviderIsService` are supported. Each request gets its own scope, and
`httpContext.GetMaxenScope()` hands you the Maxen scope for features the standard provider doesn't have.

In ASP.NET Core you also get:
```csharp
builder.AddMaxenRequestValues();   app.UseMaxenRequestValues();   // take RequestValues in any constructor: path, user, trace id
app.MapMaxenDiagnostics();                                        // registrations, graph and validation as JSON (503 if invalid)
```

## The source generator

Classes marked `[Service]` become plain `new Foo(...)` registrations at compile time. Beyond that:

- `[Decorator(typeof(IGreeter), Priority = 5)]` wraps a service, open generics included, with no `Decorate` call.
- `[Intercept(typeof(MyInterceptor), Order = 0)]` puts an interceptor in front; the interface proxy is written at compile
  time, so it is native AOT friendly. The interceptor needs a public parameterless constructor.
- For every type you register in code with `Register<T>()`, the generator writes a direct constructor call and hands it to
  `GeneratedActivators` when the assembly loads, so the container skips building expression trees. Nothing to switch on.
- It picks the `[Inject]` constructor or the one with the most parameters. Classes it cannot handle (generic, abstract,
  optional parameters) get a warning (`MAXEN001`/`MAXEN002`), and a wrong `ServiceType` is an error (`MAXEN003`). Those
  can still be registered with `RegisterAttributed` at run time.

The analyzers in the same package are covered in the [diagnostics reference](docs/diagnostics.md);
`samples/Maxen.DI.Samples.Generators` shows it all running.

## The command line tool

```bash
dotnet tool install -g Maxen.DI.Tool
maxen-di graph bin/Release/net10.0/MyApp.dll --format mermaid --validate
maxen-di validate MyApp.dll                  # CI gate: prints nothing, exits 1 on any problem
maxen-di diff old/MyApp.dll new/MyApp.dll    # registrations and dependencies that were added or removed, exits 1 if any
maxen-di explain MyApp.dll IGreeter          # every registration of a service, which one wins, what it needs, who needs it
maxen-di snapshot MyApp.dll graph.json       # save the graph; later: maxen-di diff MyApp.dll --baseline graph.json
maxen-di graph MyApp.dll --format json       # machine-readable graph
maxen-di init Billing --namespace Shop       # write a starter BillingModule.cs
```
It builds a container from the `IContainerModule` classes in the assembly and reports on that. Formats are `mermaid`, `dot`,
`text` and `json`.

## How it works

### Pipeline
Every resolve passes through: validation → pre-lifetime middleware → lifetime → post-lifetime middleware → decorators →
activation (constructor + initializers).

### Resolve plans
When a container has no middleware, decorators, initializers or custom parameter resolvers, a plain registration is
resolved from a precomputed recipe: the whole dependency tree is read once, and later top-level resolves build objects
without walking the pipeline for every dependency. Collections, `Lazy<T>`, `Func<T>` and factory registrations are
planned too. Anything the plan can't prove safe (loops, captive dependencies, conditions, open generics) uses the normal
path, so errors and behaviour are identical. `builder.Options.UseResolvePlans = false` switches it off.

### Extension points

| Point | Interface / type | Registered with |
|---|---|---|
| Lifetimes | `Lifetime` (derive) | `.WithLifetime(...)` |
| Resolution pipeline | `IResolutionMiddleware`, `MiddlewarePhase` | `builder.Use(...)`, `registration.Use(...)`, `OnActivated` |
| Dynamic registrations | `IRegistrationSource` | `builder.AddSource(...)` |
| Constructor choice | `IConstructorSelector` | `builder.ConstructorSelector = ...` |
| Parameter values | `IParameterResolver` | `builder.AddParameterResolver(...)` |
| Post-construction hooks | `IInstanceInitializer` | `builder.AddInitializer(...)` / `UsePropertyInjection()` |
| Call interception | `IInterceptor` | `.InterceptWith(...)`, `builder.Intercept<,>()` |
| Resolution observers | `IResolutionObserver` | `builder.Observe(...)` |
| Reusable configuration | `IContainerModule` | `builder.RegisterModule(...)` / `RegisterModules(assembly)` |

### Rules worth knowing

- The last registration of a service wins; `ResolveAll` returns all in registration order.
- A closed registration is merged with closed versions of matching open generics by registration order.
- Sources are only consulted when a key has no registration.
- Instances passed to `RegisterInstance` are not disposed by the container.
- Only transient services accept resolve-time arguments.
- A builder can only be built once.

### Namespaces

`Maxen.DI` is the root namespace and holds the everyday API: `ContainerBuilder`, `Container`, `IScope`, `ServiceKey`,
lifetimes, attributes, exceptions and the registration extension methods. Specialised areas have their own, matching
their folder:

| Namespace | What is in it |
|---|---|
| `Maxen.DI.Pipeline` | `IResolutionMiddleware`, `ResolveContext`, `MiddlewarePhase` |
| `Maxen.DI.Diagnostics` | `DependencyGraph`, validation, telemetry (`MaxenTelemetry`, `UseTelemetry`), resolution observers |
| `Maxen.DI.Async` | `IAsyncInitializable`, `ResolveAsync`, `InitializeSingletonsAsync` |
| `Maxen.DI.Arguments` | `ResolveArguments` |
| `Maxen.DI.Extensibility` | modules, registration sources, parameter resolvers, initializers |
| `Maxen.DI.Interception`, `Maxen.DI.Options`, `Maxen.DI.Scanning` | interception, options binding, scanning |
| `Maxen.DI.Internal` | implementation details, not for use |

## Performance and limits

Short-job BenchmarkDotNet figures from one machine (Core i5-10400T, .NET 10). Yours will differ, so run the benchmarks:

| Scenario | Maxen.DI | Microsoft | Autofac |
|---|---|---|---|
| Resolve an existing singleton | ~20 ns, 0 B | ~10 ns, 0 B | ~130 ns, 656 B |
| Scope + three-level graph | ~210 ns, 224 B | ~150-165 ns, 360 B | ~2,000 ns, 4.8 KB |
| One scope, the same scoped service asked for 10 times | ~300 ns, 200 B | ~520 ns, 336 B | not measured |

Maxen.DI is not the fastest container for every shape of work. Creating a scope and building a fresh graph takes roughly
1.3 times as long as Microsoft's, and a plain singleton lookup about twice. The extra time buys the checking and the
extension points. See the [comparison](docs/comparison.md) for the full picture.

Track regressions with `scripts/compare-benchmarks.ps1` (run the benchmarks with `--exporters json`; use `-Update`
to record a new `benchmarks/baseline.json`). The `Benchmarks` workflow runs it on demand.

## Trimming and native AOT

The core library is built with the trim/AOT analyzers on and has no warnings. Registrations through
`Register<T, TImpl>()`, factories, instances and the `[Service]` generator are safe. Features that inherently use
reflection are marked `[RequiresUnreferencedCode]`: assembly scanning, `[Inject]` properties and methods,
interception by `DispatchProxy`, and binding options from dictionaries or configuration. Open generics work for closed
types your app uses.

What has been checked: the console sample publishes with native AOT on Linux with warnings treated as errors, and the
resulting executable runs. A CI job does this on every push. The same sample also publishes with the IL trimmer
(`PublishTrimmed`) with no warnings. Not checked: a native build on Windows or macOS.

## Contributing and releasing

- **Conventions:** one type per file, file named after the type; comments explain *why*, in plain language.
- **Build and test** (you need the .NET 8, 9 and 10 SDKs):
  ```bash
  dotnet test Maxen.DI.slnx
  dotnet pack src/Maxen.DI -c Release -o artifacts
  dotnet run -c Release --project benchmarks/Maxen.DI.Benchmarks -- --filter "*"
  ```
- **Releasing:** pushing a tag such as `v1.2.0` runs `.github/workflows/release.yml`, which builds, tests, packs with that
  version and pushes to NuGet (through NuGet Trusted Publishing, so no API key is stored; setup is described at the top of the workflow file). A tag with a suffix (`v1.0.0-preview.1`) is
  published as a prerelease.

See [CHANGELOG.md](CHANGELOG.md) and [CONTRIBUTING.md](CONTRIBUTING.md).
