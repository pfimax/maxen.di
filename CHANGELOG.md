# Changelog

Newest first. Versions follow [Semantic Versioning](https://semver.org/), and pushing a tag such as `v1.2.0` is what
triggers a release (see the README).

## Unreleased

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
