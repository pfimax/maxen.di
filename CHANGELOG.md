# Changelog

Newest first. Versions follow [Semantic Versioning](https://semver.org/), and pushing a tag such as `v1.2.0` is what
triggers a release (see [CONTRIBUTING.md](CONTRIBUTING.md)).

## Unreleased

### Added
- .NET 11. All four packages now target net8.0, net9.0, net10.0 and net11.0, and the tests and Microsoft's conformance
  suite run on all four (143 conformance tests on .NET 10 and 11, 139 on .NET 8 and 9).
- More benchmarks: starting up, a graph of shared services (first resolve and `Validate()`), a decorated service,
  collections and keyed services, with Microsoft and Autofac rows where they have the feature. The figures in the
  README and on the comparison page were re-measured on one machine with full runs.
- `Validate()` checks decorators. What a decorator's constructor needs is treated as belonging to the service it wraps,
  so a decorator with an unregistered dependency is a `MissingDependency`, and a decorator on a singleton that takes a
  scoped service is a `LifetimeMismatch`. Before, both passed validation and failed on the first resolve. The drawn
  graph and saved snapshots are unchanged: they still show services only.
- Validation, the dependency graph and `maxen-di` now see what `[Service]` classes need. The source generator writes
  what each generated registration resolves onto it (`ServiceMetadata.Dependencies`), and it is checked like a
  constructor. You can add the same entry to a factory of your own with `WithMetadata`. Rebuild your project to get it.
  Graphs and snapshots of projects that use `[Service]` gain the edges that were missing, so `maxen-di diff` against an
  old snapshot will list them once.
- Analyzer `MAXEN010`: a scoped service resolved from the root container, which throws at run time. It has the same
  quick fix as `MAXEN007` (resolve from a new scope), and stays quiet when the project switches `ValidateScopes` off.
- `MAXEN005` follows transients: a singleton that reaches a scoped service through one or more transients is reported,
  with the transients named in the message.
- `maxen-di` warns on standard error when the inspected assembly was built with a different version of `Maxen.DI` than
  the tool carries, and names the command that installs the matching tool.
- Builds with `-p:ContinuousIntegrationBuild=true` (CI and releases) treat warnings as errors, and packing a library
  checks its public API against the last release, so an accidental breaking change fails the build.

### Changed
- A disposed scope or container now refuses every request with `ObjectDisposedException`: resolving any service,
  `ResolveAll`, `TryResolve`, `GetService`, and creating a scope from it. Before, only lookups that reached the scope's
  own instances threw, so a transient with nothing to dispose, or a singleton that already existed, was still handed
  out. A `Lazy<T>` or `Func<T>` used after its scope ended now throws too.
- A child container starts with all of its parent's options. Before, only `ValidateScopes` and `ValidateOnBuild` were
  copied and `ValidateFactories`, `UseResolvePlans` and `DisposeChildScopes` went back to their defaults. Set an option
  in the `CreateChild` callback if the child should differ.
- Starting up is much faster. The container used to compile a call for every constructor the first time it was used,
  which costs far more than running the constructor, and most services (singletons above all) are built too rarely
  to earn it back. A constructor is now called through reflection at first and gets its compiled call on a background
  thread after its second use, so nothing waits for compiling. Steady-state speed is unchanged, and both ways build the
  same object and throw the same exceptions.
- The analyzers now tell containers apart. A builder created with `var builder = new ContainerBuilder();` and only used
  for calls on it is checked against its own registrations. Before, every registration in a project was treated as one
  container, so a service registered on one builder hid a missing registration on another (`MAXEN006`), and a lifetime
  from one builder could raise `MAXEN005` or `MAXEN009` on another. A call the analyzer can't follow now switches
  `MAXEN006` off for the builder it is made on, not for such a builder elsewhere in the project, so you may see new,
  correct `MAXEN006` warnings in projects that build several containers, test projects especially. Registrations made
  on a parameter, a field or a builder that is passed around are checked as before.

### Fixed
- `MAXEN006` reported a service as unregistered although it was registered by `RegisterAssemblyTypes`, `Populate`,
  `TryRegister`, `RegisterFactory(typeof(IFoo), ...)`, `RegisterInstance(typeof(IFoo), ...)`, `AlsoAs(typeof(IFoo))`,
  `Add(descriptor)`, a module's `Configure(builder)` called directly, or a helper method in another project that takes
  the builder. `TryRegister` now counts as a registration; the others switch the check off, as scanning already did.
- `MapMaxenDiagnostics()` remembered a failure. If working out the report threw once, every later request got the same
  exception until the app restarted. Only a finished report is kept now, and the next request tries again.
- Two scopes resolving a pooled service for the first time at the same moment could each create a pool. The spare was
  never used but stayed with the container until it was disposed. Only one pool is created now.
- With `ValidateFactories` on, a factory registered with `AsScopedTo("request")`, or one that resolved such a service,
  was reported as `FactoryFailed` because validation ran it outside any labelled scope. Factories now run inside a
  scope for every label in use.
- When `ValidateOnBuild` failed, singletons that `ValidateFactories` had already built were never disposed, because the
  container holding them was thrown away. `Build()` now disposes the container before it throws.
- Resolving a service for the first time could take seconds and hundreds of megabytes when the services below it were
  shared by many others. The resolve plan held a shared service once for every path that led to it, so it doubled with
  each level of such a graph. A shared service is now planned once. Where that isn't possible, because a factory sits
  below it and a factory is told who is asking, a plan that passes 2,000 steps is dropped and the service is built the
  normal way, with the same result.
- `Validate()` had the same problem: its lifetime check walked everything below a shared service again for every path
  that reached it, so `ValidateOnBuild`, `maxen-di validate` and the first call to `MapMaxenDiagnostics()` could take
  seconds or never finish on a deep graph. It now walks below a service again only for a singleton (or other
  restrictive service) that has not been above it before. The issues it reports, and their order, are unchanged. One
  exception: in a graph that also has a dependency loop, a lifetime mismatch that runs through the loop may only show
  once the loop is fixed.

## 1.1.0 (2026-11-06)

### Added
- Classes written for Microsoft's container now work unchanged, whether you use the adapter or a plain `ContainerBuilder`:
  - `[FromKeyedServices("a")]` on a constructor parameter asks for the service registered under `"a"`. Written without
    a key, `[FromKeyedServices]` asks for the same key as the service being built.
  - `[ServiceKey]` on a constructor parameter receives the key the service was registered or asked for with. A class that
    also has a constructor without that parameter can still be registered without a key. If the key has the wrong type for
    the parameter, you get an error that says so.
  - `ServiceKey.AnyKey` (and Microsoft's `KeyedService.AnyKey`) registers a service for every key that has no registration
    of its own. A singleton registered this way gets one instance per key. Resolving `IEnumerable<T>` with it returns every
    keyed registration of `T`.
- `[Service]` classes may use the same attributes, and `MAXEN006` no longer warns about parameters marked with them.
- Microsoft's keyed-services conformance tests now run against the adapter too: 143 tests in all (139 on .NET 8 and 9).

### Changed
- `IEnumerable<T>`, `IReadOnlyCollection<T>` and `IReadOnlyList<T>` made up only of singletons are now built once and
  reused, as Microsoft's container does. If you resolve them as arrays, `IList<T>` or `ICollection<T>`, you still get a
  new copy every time, so changing it can't affect anyone else.
- When the Microsoft adapter can't build a service, it now throws `InvalidOperationException`, which is what code written
  for Microsoft's container catches. The Maxen exception (`ResolutionException`, `CircularDependencyException`, ...) is its
  `InnerException`. If you caught the Maxen types from an `IServiceProvider`, catch `InvalidOperationException` and look
  at `InnerException`. Resolving through Maxen's own `IScope` throws the same exceptions as before.

### Fixed
- `MapMaxenDiagnostics()` validates once, on the first request, and reuses the answer. Before, it validated on every
  request, so with `ValidateFactories` on, every health probe ran your factories again.
- `maxen-di` printed its usage text twice when arguments were missing. It now prints it once.
- `maxen-di` crashed with a stack trace when the inspected assembly's own code failed: a module that throws, a
  dependency missing from the build output, or a snapshot file in the wrong shape. These now print `Could not inspect`
  (or "not a snapshot") with the reason, and exit with code 2.
- In projects whose name starts with `Maxen.DI`, calling `RegisterGeneratedServices()` switched analyzer `MAXEN006` off.
  It now stays on, and counts your `[Service]` classes as registered.
- `GetRequiredService<X>()` said "No service for type X has been registered" when `X` was registered but one of its own
  dependencies was missing. The message now names the dependency that is missing.

### Documentation
- Eight new guides on the [documentation site](https://pfimax.github.io/maxen.di/): registering services; lifetimes,
  scopes and disposal; decorators and interception; modules, profiles and options; validation and diagnostics; ASP.NET
  Core and the Generic Host; the source generator; and the command line tool.
- `Maxen.DI.Tool`, `Maxen.DI.Extensions.DependencyInjection` and `Maxen.DI.Extensions.AspNetCore` have their own
  README on NuGet, starting with their own setup.

## 1.0.0 (2026-11-05)

The first version. Everything below is new, so there is no migration to do.

### The container
- Transient, scoped and singleton lifetimes, plus pooled, per-thread and named (`AsScopedTo("request")`) scopes, and a
  `Lifetime` base class for your own.
- Keyed services, open generics, `AlsoAs`, collections, `Lazy<T>` and `Func<..., T>` with arguments, `ResolveWith` for
  values only known at run time, conditional registrations (`WhenInjectedInto`, `When`), `Replace` and `Override`.
- Decorators with priorities (open generics included) and interceptors.
- A resolution pipeline with middleware before and after the lifetime, registration sources, constructor selectors,
  parameter resolvers, instance initializers and observers. Property and method injection are opt-in.
- Child containers, profiles, feature flags (`MAXEN_FEATURES`), modules, fluent assembly scanning and options binding.
- Async initialization (`IAsyncInitializable`, `ResolveAsync`, `InitializeSingletonsAsync`).
- Resolve plans: plain registrations are built from a precomputed dependency tree instead of walking the pipeline for
  every dependency. Collections, `Lazy<T>`, `Func<..., T>` and factories are planned too. On by default
  (`ContainerOptions.UseResolvePlans`), and tested to behave exactly like the normal path.
- Disposal: each scope disposes what it built, newest first. `ContainerOptions.DisposeChildScopes` makes a parent also
  dispose the scopes created from it (off by default).

### Finding mistakes early
- `container.Validate()` and `ContainerOptions.ValidateOnBuild`: missing dependencies, loops and captive dependencies, plus
  unreachable `WhenInjectedInto` registrations. `ValidateFactories` also runs factories once and reports the ones that throw.
- "Did you mean" suggestions on `ServiceNotRegisteredException`.
- Dependency graphs as Mermaid, DOT, text or JSON; `ResolutionTrace`; slow-resolve reporting; telemetry under the name
  `Maxen.DI` (`UseTelemetry`).
- Analyzers `MAXEN001` to `MAXEN009`, with code fixes for `MAXEN004` to `MAXEN007`. See [docs/diagnostics.md](docs/diagnostics.md).

### Source generator
- `[Service]`, `[Decorator]` (with `Priority`, open generics) and `[Intercept]` are turned into registrations and proxy
  classes at compile time, so none of it needs run-time code generation.
- For types registered in code, direct constructor calls are generated and used instead of expression trees.

### Packages
- `Maxen.DI`: the framework, with the generator, analyzers and code fixes inside.
- `Maxen.DI.Extensions.DependencyInjection`: runs Maxen.DI behind `IServiceCollection` and the Generic Host. Passes
  Microsoft's own conformance suite (89 tests). `LogResolutions` forwards resolve events to `ILogger`.
- `Maxen.DI.Extensions.AspNetCore`: `UseMaxenDI`, `RequestValues`, and `MapMaxenDiagnostics()`, a JSON endpoint with the
  registrations and validation result (503 when validation fails, so it works as a readiness probe).
- `Maxen.DI.Tool` (`maxen-di`): `graph`, `validate`, `diff` (two assemblies or one against a saved `--baseline`),
  `snapshot`, `explain` and `init`.
- Targets net8.0, net9.0 and net10.0. Root namespace `Maxen.DI`; specialised areas live in `Maxen.DI.Pipeline`,
  `.Diagnostics`, `.Async`, `.Arguments`, `.Extensibility`, `.Interception`, `.Options` and `.Scanning`.

### Samples, docs and checks
- Samples for every feature (`samples/`), including the source generator, Generic Host, ASP.NET Core and OpenTelemetry.
- Guides for getting started, extending, migrating from Microsoft DI and Autofac, the diagnostics, and a comparison page.
- Tests: Microsoft's conformance suite, 300 random registration graphs checked against a plain reference model, every
  lifetime combination of a six-class graph (729) run with plans on and off, Kestrel end-to-end tests, and fuzzing of
  the proxy generator. `scripts/verify-packages.ps1` installs the packed packages into a clean project.
- Dependencies are on their current versions. Roslyn (`Microsoft.CodeAnalysis.CSharp` 4.11) is pinned on purpose so the
  generator and analyzers still load in older compilers and Visual Studio versions.
- CI: build and test on all three target frameworks, coverage, a benchmark check against `benchmarks/baseline.json`, a
  native AOT publish, and a DocFX site on GitHub Pages.

### Known gaps
- Native AOT is checked on Linux only (the CI job publishes the console sample and runs it). Windows and macOS builds
  have not been tried.
- The compile-time dependency graph (validating the graph during the build, not at startup) is not done.
