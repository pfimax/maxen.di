# How Maxen.DI compares

This page puts Maxen.DI next to the two containers most .NET teams already know: `Microsoft.Extensions.DependencyInjection`
(MS.DI) and Autofac. Every Maxen.DI claim names the test that proves it, so you can run it yourself:

```bash
dotnet test tests/Maxen.DI.Tests -c Release \
  --filter "FullyQualifiedName~DecoratorTests"
```

The MS.DI and Autofac columns describe their features as we understand them from their public documentation; containers
change, so check their current docs before relying on a "no". "Add-on" means a separate package or helper is needed.

## Features

| Feature | Maxen.DI | MS.DI | Autofac | Proof in this repo |
|---|---|---|---|---|
| Transient, scoped, singleton | yes | yes | yes (+ per-match scopes) | `LifetimeTests` |
| Custom and pooled lifetimes | yes | no | yes (custom) | `PooledLifetimeTests`, `ExtensibilityTests` |
| Scope tags (`AsScopedTo`) | yes | no | yes (tagged scopes) | `TaggedScopeTests` |
| Child containers that inherit and override | yes | no | child lifetime scopes | `ChildContainerTests` |
| Keyed services | yes, Microsoft's attributes and `AnyKey` included | yes (.NET 8+) | yes | `KeyedTests`, `KeyedCompatibilityTests` |
| Open generics | yes | yes | yes | `GenericTests` |
| `IEnumerable<T>` / arrays of all registrations | yes | yes | yes | `CollectionTests` |
| `Lazy<T>` and `Func<..., T>` with arguments | built in | no | yes | `ResolveArgumentsTests`, `ResolvePlanTests` |
| Resolve with runtime arguments | yes | add-on (`ActivatorUtilities`) | yes (parameters) | `ResolveArgumentsTests` |
| Decorators | built in, open generics too | add-on (Scrutor) | built in | `DecoratorTests`, `SourceGeneratorTests` |
| Interception | built in; source-generated proxies | no | add-on (Castle) | `InterceptionTests`, `GeneratedProxyTests`, `ProxyGeneratorFuzzTests` |
| Conditional registration (`WhenInjectedInto`) | yes | no | via `WithParameter`/filters | `ConditionalRegistrationTests` |
| Property and method injection | opt-in | no | yes | `MethodInjectionTests` |
| Profiles and feature flags for modules | yes | no | modules, no flags | `ProfileTests`, `FeatureTests` |
| Async initialization | yes | no | no | `AsyncInitializationTests` |
| Assembly scanning | yes | add-on (Scrutor) | yes | `ScanningTests`, `FluentScanningTests` |
| Runs behind the Microsoft abstractions / Generic Host | yes | n/a | yes (adapter) | `MicrosoftAdapterTests`, `AspNetCoreEndToEndTests` |

## What is different in kind

These are the things Maxen.DI does that the others do not do out of the box.

| Capability | Maxen.DI | MS.DI | Autofac | Proof |
|---|---|---|---|---|
| Whole-graph validation without building anything (missing dependency, loop, captive lifetime) | `container.Validate()` | partly (`ValidateOnBuild`) | no | `ContainerValidationTests`, `RandomGraphTests` |
| Compile-time diagnostics (MAXEN001-010) while you type | yes | no | no | `BuilderCallAnalyzerTests`, `GeneratorDiagnosticsTests` |
| Code fixes for those diagnostics | MAXEN004-007 and 010 | no | no | `CaptiveLifetimeCodeFixTests`, `DiagnosticCodeFixTests` |
| Source-generated registrations, activators and proxies (no reflection on that path) | yes | no | no | `SourceGeneratorTests`, `GeneratedActivatorTests` |
| Dependency graph as Mermaid, DOT, text or JSON, diffed between builds or against a saved baseline | `maxen-di` tool | no | no | `ToolTests` |
| Built-in traces and metrics under one name (`Maxen.DI`) | yes | no | no | `TelemetryTests`, `samples/Maxen.DI.Samples.OpenTelemetry` |
| Health endpoint with graph and validation result | `MapMaxenDiagnostics()` | no | no | `AspNetCoreEndToEndTests` |
| Trim and native AOT friendly core | analyzers on, 0 warnings; native AOT sample published and run in CI | yes | limited (reflection-heavy) | CI `aot` job |

## Correctness evidence

- **Microsoft's own conformance suite** (`Microsoft.Extensions.DependencyInjection.Specification.Tests`) runs against the
  Maxen adapter, the keyed-services suite included: 143 tests on net10 and net11, and 139 on net8 and net9 because
  four of the keyed tests only exist from .NET 10 on (`Maxen.DI.Specification.Tests`).
- **Randomized comparison:** 300 random registration graphs are checked against a plain reference model for captive
  detection, sharing per lifetime and disposal exactly once (`RandomGraphTests`).
- **Fast path equals slow path:** the precomputed resolve plans are compared with the full pipeline over 729
  registration combinations (`PlanComparisonTests`, `PlanCollectionComparisonTests`, `ResolvePlanTests`).
  `MAXEN_EXHAUSTIVE=1` runs every combination.
- **Real HTTP:** Kestrel end-to-end tests cover scoped sharing between middleware and endpoints, controllers, hosted
  services, keyed services and endpoint filters (`AspNetCoreEndToEndTests`).

## Speed, honestly

BenchmarkDotNet figures (default job) from one machine (Core i5-11400F, .NET 10); yours will differ. Reproduce them
with `benchmarks/Maxen.DI.Benchmarks`.

| Scenario | Maxen.DI | MS.DI | Autofac |
|---|---|---|---|
| Resolve an existing singleton | ~13 ns, 0 B | ~6 ns, 0 B | ~70 ns, 656 B |
| Scope + three-level graph | ~115 ns, 232 B | ~85 ns, 360 B | ~1,230 ns, 4.8 KB |
| One scope, same scoped service 10 times | ~180-260 ns, 208 B | ~295 ns, 336 B | ~1,680 ns, 9.9 KB |
| Startup: build a container and serve the first request | ~3.8 µs, 13 KB | ~2.2 µs, 8 KB | ~14.9 µs, 37 KB |
| Every registration of a service (three singletons) | ~16 ns, 0 B | ~8 ns, 0 B | ~415 ns, 1.2 KB |
| A keyed singleton | ~19 ns, 0 B | ~11 ns, 0 B | ~75 ns, 664 B |

Three more Maxen.DI figures have no counterpart in the other two:

| Scenario | Maxen.DI |
|---|---|
| Scope + three-level graph, with a decorator on the service | ~680 ns, 1.5 KB |
| First resolve in a graph of shared services (13 services, 4,096 paths to the bottom) | ~27 µs, 61 KB |
| `Validate()` on that same graph | ~24 µs, 52 KB |

Maxen.DI is **not** the fastest container: MS.DI is about twice as fast for a single lookup (a singleton, a
collection, a keyed service), and faster for a scope that builds a fresh graph and for starting up. Maxen.DI wins when a
scoped service is asked for repeatedly, allocates less per scope, and is far ahead of Autofac. A decorator takes a
service off the fast path, which is the difference between the second row and the decorated one. The choice is about
features and diagnostics, not raw speed.

`benchmarks/baseline.json` is a different set of numbers: the Maxen.DI figures as measured on the CI machine, which is
slower than the one above. CI compares each run against it to catch a regression.

## When to stay where you are

- You only need the basics and want the smallest dependency: MS.DI is enough.
- You lean on Autofac-specific features, such as its integration packages: check the
  [migration guide](migrating.md) before moving.
