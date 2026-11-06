# Validation and diagnostics

Maxen.DI tries to tell you about a wiring mistake before a user runs into it. This page covers the checks you can run
at startup, the errors you get when something slips through to resolve time, and the tools for seeing what the
container is doing: the dependency graph, resolution observers and OpenTelemetry.

## Three places to catch a mistake

The same kinds of mistake (a missing registration, a loop, a singleton holding on to a scoped service) can be caught
at three points:

- **While you type.** The analyzers in the `Maxen.DI` package flag missing registrations, captive dependencies and
  more in your editor, many with a quick fix. [Diagnostics](diagnostics.md) lists every one.
- **At startup.** `container.Validate()` reads the whole configuration and reports every problem, without building
  a single service. This page is mostly about that.
- **In CI.** `maxen-di validate YourApp.dll` runs the same checks against the `IContainerModule` classes in your
  assembly and fails the build. See [the command line tool](command-line-tool.md).

The analyzers only see what the compiler sees, and the tool only sees modules. Startup validation sees the container
you actually built, so it is the one to rely on.

## Validating at startup

```csharp
using Maxen.DI.Diagnostics;

await using var container = builder.Build();

foreach (var issue in container.Validate())
    Console.WriteLine(issue);
```

`Validate()` on the <xref:Maxen.DI.Container> returns a list of `ValidationIssue` records. An empty list means it found
nothing wrong. Each issue has a `Kind`, the `Service` it belongs to and a `Message`, and `ToString()` prints them as
`[Kind] message`.

`EnsureValid()` runs the same checks and throws a `ContainerValidationException` if anything turns up. Its message
lists every issue, not only the first, and the `Issues` property holds them as a list.

To make `Build()` itself refuse a broken configuration, switch on `ValidateOnBuild`:

```csharp
var builder = new ContainerBuilder();
builder.Options.ValidateOnBuild = true;

// Throws ContainerValidationException listing every problem
var container = builder.Build();
```

This is the easiest place to put it in a real application: a bad deployment fails as it starts, not on the first
request that happens to need the broken service.

### What it checks

Validation builds a picture of who needs whom from the constructors, then looks for:

| `ValidationIssueKind` | What it means |
|---|---|
| `MissingDependency` | A constructor needs something that isn't registered, or no constructor can be chosen (for example two equally long ones). |
| `CircularDependency` | Services need each other in a loop. The message shows the loop, such as `A -> B -> A`. Each loop is reported once. |
| `LifetimeMismatch` | A longer-lived service depends on a shorter-lived one, such as a singleton on a scoped service. This is found through transients in between too, and the message shows the path. |
| `UnreachableRegistration` | A `.WhenInjectedInto<X>()` registration where nothing registered is an `X`, so it can never be used. A child container may name a consumer registered in its parent. |
| `FactoryFailed` | A factory registration threw when validation ran it. Only reported with `ValidateFactories` on. |

`Lazy<T>` and `Func<T>` dependencies are resolved later, not while the service is being built, so they don't count
towards loops or captive dependencies. `LifetimeMismatch` is only checked while `ValidateScopes` is on, which it is by
default.

### What it can't see

Validation reads constructors. It doesn't build anything, so some things stay invisible to it:

- **Factories are opaque.** A service registered with `RegisterFactory` is a leaf in the graph: validation can't see
  what the lambda will ask for. Instances are the same. `ValidateFactories`, below, is the way round that.
- **Values supplied at resolve time** look like missing dependencies. If a service takes arguments through
  `ResolveWith` or a `Func<string, T>`, mark its registration so validation leaves those parameters alone:

  ```csharp
  builder.Register<Report>().WithRuntimeArguments();
  ```

- Decorators and property injection are not part of the graph.

## The scope and factory options

These live on `builder.Options`, a <xref:Maxen.DI.ContainerOptions>:

| Option | Default | What it does |
|---|---|---|
| `ValidateOnBuild` | off | `Build()` calls `EnsureValid()` and throws on any problem. |
| `ValidateScopes` | on | Rejects scoped services resolved from the root container and longer-lived services that hold shorter-lived ones, at resolve time and in validation. |
| `ValidateFactories` | off | Validation also runs each factory registration once and reports the ones that throw. |

`ValidateFactories` exists because a factory can ask for something that was never registered, and reading
constructors will never find that:

```csharp
builder.Options.ValidateFactories = true;
builder.RegisterFactory<IGreeter>(
    ctx => new Greeter(ctx.Container.Resolve<IClock>()));

// [FactoryFailed] The factory for IGreeter threw
// ServiceNotRegisteredException: No registration found for 'IClock'.
var issues = container.Validate();
```

Each factory runs from a throwaway scope, so scoped factories work. The factories run for real, though: whatever
side effects they have happen, and a singleton built this way stays cached. Leave it off if your factories open
connections, send messages or do anything else you wouldn't want at startup. The factories only run when the
constructor checks found nothing, since there is no point running them against a graph already known to be broken.

## Errors at resolve time

When something does go wrong while resolving, every exception derives from `DIException`, so one `catch` can handle
them all. The ones you are most likely to meet:

| Exception | When | What the message contains |
|---|---|---|
| `ServiceNotRegisteredException` | Nothing is registered for the service you asked for. | The service, the resolution path that led to it, and "Did you mean ..." suggestions. |
| `CircularDependencyException` | Building a service needs that same service. | The loop, such as `Circular dependency detected: A -> B -> A`. |
| `ScopeValidationException` | A scoped service resolved from the root, or a singleton that needs a scoped service. | The lifetimes, both services and the dependency path. |
| `ResolutionException` | Anything else that stops a service being built. | What went wrong and, where it applies, the path. |
| `ContainerValidationException` | `EnsureValid()` or `ValidateOnBuild` found problems. | Every issue, one per line. |

`CircularDependencyException`, `ScopeValidationException` and `ServiceNotRegisteredException` all derive from
`ResolutionException`. `ContainerValidationException` is in `Maxen.DI.Diagnostics`; the others are in `Maxen.DI`.

`ResolutionException` covers cases such as a constructor whose parameters can't be satisfied ("Unable to select a
constructor for 'Greeter'. Unresolvable parameters: IClock clock."), two constructors of the same length, a factory
that returned null, arguments passed to a service that isn't transient, and asking for a single service with
`ServiceKey.AnyKey`. An exception thrown inside your own factory is not wrapped; it reaches you as it was.

### "Did you mean"

A missing service is often a near miss rather than a missing registration. `ServiceNotRegisteredException` looks
through what is registered and suggests up to three things:

- the same service registered under a key, or without one, when you used the other;
- an interface the class you asked for was registered under ("resolve that instead");
- a service with the same name in another namespace, or a name a couple of letters away.

```csharp
try
{
    scope.Resolve<IGreeter>();
}
catch (ServiceNotRegisteredException ex)
{
    // e.g. "'IGreeter (key: plain)' is registered with key 'plain'"
    foreach (var hint in ex.Suggestions) Console.WriteLine(hint);
}
```

`Key` holds the service that couldn't be found. `Suggestions` is often empty, when nothing registered looks close.

## The dependency graph

`container.BuildGraph()` returns the same picture validation works from, a `DependencyGraph`, without building any
service. Use it to document an application or to see why something depends on something else.

```csharp
var graph = container.BuildGraph();

File.WriteAllText("graph.md", graph.ToMermaid());
File.WriteAllText("graph.dot", graph.ToDot());
Console.WriteLine(graph.ToText());
```

`ToMermaid()` writes a Mermaid flowchart, `ToDot()` a Graphviz digraph, and `ToText()` one registration per line
followed by what it needs. In the diagrams a dotted or dashed arrow is a deferred dependency (`Lazy<T>` or
`Func<T>`).

For your own tooling, `Nodes` lists every registration, `Edges` every dependency as a `DependencyEdge` (`From`, `To`,
`Deferred`), and `EdgesFrom(node)` the dependencies of one registration. `ConstructionIssues` holds constructors that
couldn't be chosen. The library has no JSON export of the graph; `maxen-di graph --format json` in the
[command line tool](command-line-tool.md) has one, and so does the ASP.NET Core endpoint in
[ASP.NET Core](aspnetcore.md).

Like validation, the graph treats factories and instances as leaves and leaves out decorators and property
injection.

## Watching resolutions

An observer is told about every resolution once it has finished, successful or not. The built-in one,
`ResolutionTrace`, just remembers them, which helps in tests and when you are asking yourself what got built and
why.

```csharp
var trace = new ResolutionTrace();
builder.Observe(trace);

using var container = builder.Build();
container.Resolve<UserRepository>();

Console.Write(trace);
```

`ToString()` prints an indented tree, parents before their dependencies, with the lifetime and the time each took.
Failures end with `FAILED:` and the exception type. `Events` gives you the same as a list, and `Clear()` empties it.

Each event is a `ResolutionEvent` with `Sequence`, `Key`, `Lifetime`, `Depth` (0 for the service you asked for),
`Duration` (including its dependencies), `Error` and `Succeeded`.

For your own handling, pass a lambda or implement `IResolutionObserver`:

```csharp
builder.Observe(e => Console.WriteLine($"{e.Key} {e.Duration}"));

// Only the ones that took 5 ms or more
builder.ObserveSlowResolutions(
    TimeSpan.FromMilliseconds(5),
    e => logger.LogWarning("Slow: {Service}", e.Key));
```

`IResolutionObserver` has one method, `OnResolved(ResolutionEvent)`. It is called on whichever thread did the
resolving, so make it thread-safe. `DelegateResolutionObserver` is the class the lambda overload wraps your
delegate in.

Observers see cached hits as well as fresh builds, which means singletons lose their fast path while an observer is
attached. That is fine for development and tests; think about it before leaving one on in production. If you are
running behind `IServiceCollection`, `LogResolutions` sends the same events to an `ILogger`; see
[ASP.NET Core](aspnetcore.md#logging-resolutions).

## Telemetry and OpenTelemetry

`UseTelemetry()` reports every resolution through `System.Diagnostics`, as spans and as metrics:

```csharp
builder.UseTelemetry();

// Or only one of the two
builder.UseTelemetry(tracing: true, metrics: false);
```

Nothing is recorded until something listens to the name in `MaxenTelemetry.Name`, which is `"Maxen.DI"`. It is the
name of both the activity source and the meter. With OpenTelemetry:

```csharp
using var tracing = Sdk.CreateTracerProviderBuilder()
    .AddSource(MaxenTelemetry.Name)
    .AddConsoleExporter()
    .Build();

using var metrics = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MaxenTelemetry.Name)
    .AddConsoleExporter()
    .Build();
```

What you get:

| Kind | Name | Tags |
|---|---|---|
| Span | `maxen.di.resolve` | `maxen.service`, `maxen.lifetime`, `maxen.depth`; on failure also `exception.type` and an error status |
| Counter | `maxen.di.resolutions` | `maxen.service`, `maxen.lifetime`, `outcome` (`success` or `error`) |
| Histogram (ms) | `maxen.di.resolution.duration` | `maxen.service`, `maxen.lifetime`, `outcome` |

Spans nest like the dependency chain, so a service's span contains the spans of what it needed. The duration
includes dependencies. `MaxenTelemetry.ActivitySource` and `MaxenTelemetry.Meter` are public if you want to listen
without OpenTelemetry.

Like observers, telemetry sees cached hits, so singletons lose their fast path while it is on.

## If you suspect the fast path

For plain registrations, Maxen.DI resolves from a precomputed recipe instead of walking the full pipeline for every
dependency. Anything the recipe can't prove safe uses the normal path, and the results are meant to be identical. If
you are chasing a problem and want to rule it out, switch it off:

```csharp
builder.Options.UseResolvePlans = false;
```

If that changes the behaviour, please report it, because it is a bug. Containers with middleware, decorators,
initializers, custom parameter resolvers, observers or telemetry don't use the recipes anyway.

## Samples

- [Validation](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Validation):
  `Validate()` and `ValidateOnBuild` on a broken configuration.
- [Graph](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Graph): Mermaid and text
  output.
- [Observability](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Observability):
  a `ResolutionTrace` and an `ActivityListener` on the telemetry.
- [OpenTelemetry](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.OpenTelemetry): traces and
  metrics exported to the console.
