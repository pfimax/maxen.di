# How Maxen.DI compares

This page puts Maxen.DI next to the two containers most .NET teams already know: `Microsoft.Extensions.DependencyInjection`
(MS.DI) and Autofac. Every Maxen.DI claim names the test that proves it, so you can run it yourself:

```bash
dotnet test tests/Maxen.DI.Tests -c Release --filter "FullyQualifiedName~DecoratorTests"
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
| Keyed services | yes | yes (.NET 8+) | yes | `KeyedTests` |
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
| Compile-time diagnostics (MAXEN001-009) while you type | yes | no | no | `BuilderCallAnalyzerTests`, `GeneratorDiagnosticsTests` |
| Code fixes for those diagnostics | MAXEN004-007 | no | no | `CaptiveLifetimeCodeFixTests`, `DiagnosticCodeFixTests` |
| Source-generated registrations, activators and proxies (no reflection on that path) | yes | no | no | `SourceGeneratorTests`, `GeneratedActivatorTests` |
| Dependency graph as Mermaid, DOT, text or JSON, diffed between builds or against a saved baseline | `maxen-di` tool | no | no | `ToolTests` |
| Built-in traces and metrics under one name (`Maxen.DI`) | yes | no | no | `TelemetryTests`, `samples/Maxen.DI.Samples.OpenTelemetry` |
| Health endpoint with graph and validation result | `MapMaxenDiagnostics()` | no | no | `AspNetCoreEndToEndTests` |
| Trim and native AOT friendly core | analyzers on, 0 warnings; trimmed sample runs; native publish is a CI job | yes | limited (reflection-heavy) | CI `aot` job |

## Correctness evidence

- **Microsoft's own conformance suite** (`Microsoft.Extensions.DependencyInjection.Specification.Tests`) runs against the
  Maxen adapter: 89 tests on net8, net9 and net10 (`Maxen.DI.Specification.Tests`).
- **Randomized comparison:** 300 random registration graphs are checked against a plain reference model for captive
  detection, sharing per lifetime and disposal exactly once (`RandomGraphTests`).
- **Fast path equals slow path:** the precomputed resolve plans are compared with the full pipeline over 729
  registration combinations (`PlanComparisonTests`, `PlanCollectionComparisonTests`, `ResolvePlanTests`).
  `MAXEN_EXHAUSTIVE=1` runs every combination.
- **Real HTTP:** Kestrel end-to-end tests cover scoped sharing between middleware and endpoints, controllers, hosted
  services, keyed services and endpoint filters (`AspNetCoreEndToEndTests`).

## Speed, honestly

Short-job BenchmarkDotNet figures from one machine (Core i5-10400T, .NET 10); yours will differ. Reproduce them with
`benchmarks/Maxen.DI.Benchmarks`.

| Scenario | Maxen.DI | MS.DI | Autofac |
|---|---|---|---|
| Resolve an existing singleton | ~20 ns, 0 B | ~10 ns, 0 B | ~130 ns, 656 B |
| Scope + three-level graph | ~210 ns, 224 B | ~150-165 ns, 360 B | ~2,000 ns, 4.8 KB |
| One scope, same scoped service 10 times | ~300 ns, 200 B | ~520 ns, 336 B | not measured |

Maxen.DI is **not** the fastest container: MS.DI is faster for singletons and for a scope that builds a fresh graph.
Maxen.DI wins when a scoped service is asked for repeatedly, allocates less per scope, and is far ahead of Autofac. The
choice is about features and diagnostics, not raw speed. These numbers were re-measured after the last change to resolve
plans, and `benchmarks/baseline.json` holds the same run.

## When to stay where you are

- You only need the basics and want the smallest dependency: MS.DI is enough.
- You lean on Autofac-specific features, such as its integration packages: check the
  [migration guide](migrating.md) before moving.
