# Lifetimes, scopes and disposal

A lifetime decides how long an object the container built is kept and who gets to share it. This page covers the
built-in lifetimes, the rules about which lifetimes may depend on which, scopes, how the container disposes what it
built, and services that need to `await` something before they can be used.

## Transient, scoped and singleton

```csharp
// No lifetime given, so transient.
builder.Register<IMailer, SmtpMailer>();
builder.Register<IOrderService, OrderService>().AsScoped();
builder.Register<IClock, SystemClock>().AsSingleton();
```

- **Transient** (`AsTransient()`, the default): a new object every time one is asked for. Use it for lightweight,
  stateless services and anything that must not be shared.
- **Scoped** (`AsScoped()`): one object per scope, shared by everything resolved from that scope. A scope is usually
  one web request, one message from a queue or one unit of work, so this is the lifetime for a database session or a
  per-request context.
- **Singleton** (`AsSingleton()`): one object for the whole container. Use it for services that are expensive to
  create and safe to share between threads. It is created once, even when several threads ask for it at the same
  time.

You create scopes from the container, and from other scopes:

```csharp
await using var container = builder.Build();

using (var scope = container.CreateScope())
{
    var orders = scope.Resolve<IOrderService>();
}
```

Every scope owns its own scoped instances, wherever it was created from. A scope created from another scope does not
share the parent's scoped services; only [named scopes](#named-scopes) do that.

A singleton is always built against the root container, whichever scope asked for it first, so its own dependencies
never end up tied to a scope that ends before it does.

## What may depend on what

A service must not hold on to something that lives shorter than it does. The classic mistake is a singleton that
takes a scoped service in its constructor: the singleton keeps that one instance forever, long after its scope has
been disposed. This is called a captive dependency.

Maxen.DI rejects it. Resolving such a singleton throws a `ScopeValidationException` with the whole dependency path:

```text
Singleton service 'ReportCache' cannot depend on Scoped service
'IDbSession'. Path: ReportCache -> ReportBuilder -> IDbSession
```

The check follows the whole chain, so a transient in between doesn't hide the problem. A singleton must not depend on
any lifetime that is tied to something narrower than the container: scoped, `AsScopedTo`, pooled and per-thread
services. Scoped and transient services may depend on anything.

The same rule stops you resolving a scoped (or pooled) service from the root container, where it would quietly become
a singleton:

```text
Cannot resolve scoped service 'IDbSession' from the root container.
Create a scope first. Path: IDbSession
```

You don't have to wait until a service is resolved to find out. `builder.Options.ValidateOnBuild = true` checks the
whole graph in `Build()`, and the analyzers `MAXEN004` and `MAXEN005` warn while you type. See
[Validation and diagnostics](validation-and-diagnostics.md) and [Diagnostics](diagnostics.md).

### Turning scope validation off

Both checks are controlled by `ContainerOptions.ValidateScopes`, which is on by default:

```csharp
builder.Options.ValidateScopes = false;
```

With it off, a scoped service resolved from the root is cached in the root and lives until the container is disposed,
like a singleton. Microsoft's provider only validates scopes when asked (the host asks in the Development environment),
so code moved from it sometimes relies on this.
Turn it off only while you fix such code; see [Moving to Maxen.DI](migrating.md).

## Named scopes

Sometimes "one per scope" is not precise enough. A request may open smaller scopes for each step, and you want one
instance per request that all those steps share. Give the outer scope a label and register the service against it:

```csharp
builder.Register<RequestContext>().AsScopedTo("request");

using var request = container.CreateScope("request");
using var step = request.CreateScope();

// Same instance: the step shares the request's.
var a = request.Resolve<RequestContext>();
var b = step.Resolve<RequestContext>();
```

The instance belongs to the nearest enclosing scope with that label, however deeply nested the asking scope is. Two
separate `"request"` scopes get separate instances. Any object can be a label; labels are compared with `Equals`.

The instance is built against the labelled scope and disposed when that scope is disposed, not when the nested one is.
Resolving it where no scope with the label is active throws a `ScopeValidationException` that tells you to create one
with `CreateScope("request")`. Singletons can't depend on it.

## Pooled

`AsPooled` works like scoped, one instance per scope, except that when the scope ends the instance goes back into a
pool for a later scope instead of being thrown away. It suits objects that are expensive to build and cheap to clean,
such as large buffers or parsers.

```csharp
builder.Register<ParseBuffer>().AsPooled(maxRetained: 8);

public class ParseBuffer : IPoolable
{
    public void Reset() { /* clear the state */ }
}
```

- Within a scope you always get the same instance. Scopes that are alive at the same time get different ones.
- When the scope ends, `IPoolable.Reset()` is called if the class implements it, so the next scope starts clean.
- `maxRetained` (16 when you leave it out) is how many idle instances the pool keeps per registration. When the pool
  is already full, the returned instance is disposed instead.
- Idle instances are disposed when the container is disposed.
- Like scoped services, pooled ones can't be resolved from the root container while scope validation is on, and
  singletons can't depend on them.

## Per thread

`AsPerThread()` gives one instance per thread. It is for objects that are costly to create but not thread-safe, such
as a random number generator or a formatter.

```csharp
builder.Register<IdFormatter>().AsPerThread();
```

Per-thread instances are disposed with the container, not with a scope, because threads don't end when scopes do. An
instance stays alive until the container is disposed, even after its thread has finished. Singletons can't depend on
per-thread services, since they would keep whichever thread's instance they were built with.

See the [Lifetimes sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Lifetimes).

## Custom lifetimes

When none of these fit, derive from <xref:Maxen.DI.Lifetime> and attach it with `WithLifetime(...)`. A lifetime
decides whether to return a cached instance or build a new one, and says through `IsScopeBound` and `CanDependOn`
whether longer-lived services may hold on to it. [Extending Maxen.DI](extending.md) has a worked example.

## Disposal

Each scope disposes what it built when it is disposed, newest first, so a service is disposed before the services it
depends on. The container is a scope too; disposing it disposes the singletons and anything else the root built.

Who counts as "what it built":

- A scoped service belongs to its scope, and a singleton to the container.
- A transient belongs to the scope it was resolved from. A disposable transient resolved from the root container is
  kept until the container is disposed, which is a slow leak if you do it per request. Analyzer `MAXEN007` warns about
  it.
- A transient built for a singleton belongs to the container, because singletons are built against the root.

The container does not dispose objects passed to `RegisterInstance`, or anything from a registration marked
`ExternallyOwned()`. You created those, or someone else owns them, so disposing them is up to you.

Resolving anything scoped from a scope that has been disposed throws an `ObjectDisposedException`.

### Synchronous and asynchronous disposal

Scopes and the container implement both `IDisposable` and `IAsyncDisposable`. Prefer `await using` when any of your
services implement `IAsyncDisposable`:

```csharp
await using var scope = container.CreateScope();
```

`DisposeAsync` calls `DisposeAsync` on services that have it and `Dispose` on the rest. The synchronous `Dispose` calls
`Dispose` where it exists. For a service that implements only `IAsyncDisposable`, it calls `DisposeAsync` and blocks
until it has finished. Blocking on async work like this can deadlock where there is a synchronisation context, as in
some UI frameworks, so use `await using` there.

If one service throws while being disposed, the rest are still disposed. The errors are thrown together afterwards as
an `AggregateException`.

### Child scopes

Disposing a scope does not dispose the scopes created from it. Each owner disposes its own, which is what you want
when a short-lived parent hands work to scopes that outlive it.

If you would rather the parent cleaned up after its children, switch it on:

```csharp
builder.Options.DisposeChildScopes = true;
```

Then disposing a scope, or the container, first disposes every scope created from it that is still alive, newest
first, and then its own instances. A scope that was already disposed on its own is not disposed twice. Creating a
scope from a disposed scope throws an `ObjectDisposedException` while this option is on.

See the [Disposal sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Disposal).

## Async initialization

Constructors can't be async, but some services need to open a connection or load a cache before they are usable.
Implement <xref:Maxen.DI.Async.IAsyncInitializable> and resolve them with `ResolveAsync`:

```csharp
using Maxen.DI.Async;

public class ProductCache : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken token)
    {
        // load the products
    }
}

var cache = await scope.ResolveAsync<ProductCache>();
```

`ResolveAsync` resolves the service and waits for its `InitializeAsync`, which runs once per instance however many
callers ask, including callers that ask at the same time. If it throws or is cancelled, the next `ResolveAsync`
tries again. A plain `Resolve` does not run it.

It is the object you get back that is initialized, not its dependencies. If you put a decorator around such a
service, the decorator should forward `InitializeAsync` to the service it wraps.

To pay the start-up cost before the first request rather than during it, initialize the singletons up front:

```csharp
await container.InitializeSingletonsAsync();
```

This builds every singleton registered by type that implements `IAsyncInitializable` and initializes it. A singleton
is initialized after the async-initializable singletons it takes in its constructor, so its `InitializeAsync` can use
them. Singletons registered with a factory are not included, because the container can't see what type the factory
returns; resolve those with `ResolveAsync` instead.

See the [AsyncInit sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/AsyncInit).
