# Decorators and interception

Sometimes you want to add behaviour to a service without changing the service itself: logging, caching, retries,
auditing, timing. Maxen.DI gives you two ways to do it. A decorator is a class that implements the same interface and
wraps the real one. An interceptor is a single class that sits in front of every method call on a service, whatever the
interface. This page covers both, shows a lighter option for simple cases, and ends with how to choose.

## Decorators

A decorator implements the service it wraps and takes that service as a constructor parameter. It does its own work and
passes the call on.

```csharp
public sealed class LoudGreeter : IGreeter
{
    private readonly IGreeter _inner;

    public LoudGreeter(IGreeter inner) => _inner = inner;

    public string Greet() => _inner.Greet().ToUpperInvariant();
}

builder.Register<IGreeter, Greeter>().AsSingleton();
builder.Decorate<IGreeter, LoudGreeter>();

// A LoudGreeter wrapping a Greeter.
var greeter = container.Resolve<IGreeter>();
```

The decorator keeps the lifetime of the registration it wraps. Here `Greeter` is a singleton, so the `LoudGreeter`
around it is built once and reused too. The container disposes both the decorator and the instance inside it.

Decorators aren't limited to interfaces. Any service type works, as long as the decorator derives from it or
implements it.

### Decorators with their own dependencies

Only the parameter whose type is the decorated service receives the inner instance. Every other constructor parameter
is resolved from the container like any other dependency.

```csharp
public sealed class CachingProducts : IProducts
{
    private readonly IProducts _inner;
    private readonly IMemoryCache _cache;

    public CachingProducts(IProducts inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    // ...
}
```

Because the decorator lives as long as the service it wraps, the usual lifetime rules apply to its dependencies: a
decorator on a singleton can't take a scoped service. `Validate()` checks both that and that everything the decorator
needs is registered. A decorator whose constructor has no parameter for the service it
wraps is reported by analyzer `MAXEN008` while you type; see [Diagnostics](diagnostics.md).

### Stacking and priority

You can register several decorators for the same service. By default the first one registered sits closest to the
real service, and each later one wraps the result.

```csharp
builder.Decorate<IGreeter, ExcitedGreeter>();
builder.Decorate<IGreeter, LoudGreeter>();

// LoudGreeter(ExcitedGreeter(Greeter)): "hello" becomes "HELLO!"
```

When registration order isn't in your hands, for example because the decorators come from different modules, pass a
`priority`. A lower number sits closer to the service. Decorators with the same priority keep their registration
order. The default is 0.

```csharp
// Registered first, but told to sit on the outside.
builder.Decorate<IGreeter, ExcitedGreeter>(priority: 10);
builder.Decorate<IGreeter, LoudGreeter>(priority: 0);

// ExcitedGreeter(LoudGreeter(Greeter))
```

### Which registrations get decorated

A decorator applies to every registration of the service type: the default one, keyed ones, and every item
`ResolveAll` returns. To limit it, pass a `when` check. It receives the <xref:Maxen.DI.ServiceDescriptor> of each
registration.

```csharp
builder.Register<IGreeter, Greeter>().WithKey("plain");
builder.Register<IGreeter, QuietGreeter>().WithKey("quiet");

// Only the "quiet" registration is wrapped.
builder.Decorate<IGreeter, LoudGreeter>(
    when: d => Equals(d.Key.Key, "quiet"));
```

There is also a non-generic overload, `builder.Decorate(serviceType, decoratorType, when, priority)`, for when you only
have `Type` objects.

### Open generic decorators

Pass open generic types and the decorator is closed to match each registration. It applies to the open registration
and to any closed registration of the same generic interface.

```csharp
builder.Register(typeof(IRepo<>), typeof(Repo<>));
builder.Decorate(typeof(IRepo<>), typeof(LoggingRepo<>));

// LoggingRepo<Customer> wrapping Repo<Customer>.
var repo = scope.Resolve<IRepo<Customer>>();
```

Service and decorator must both be open or both be closed; mixing them throws a `RegistrationException`.

### The `[Decorator]` attribute

If you use the [source generator](source-generator.md), you can mark the class instead of calling `Decorate`:

```csharp
[Decorator(typeof(IGreeter), Priority = 5)]
public sealed class LoudGreeter : IGreeter
{
    // ...
}
```

The generator writes the matching `builder.Decorate(...)` call into `RegisterGeneratedServices()`, open generics
included. `RegisterAttributed(assembly)` picks the attribute up at run time as well. `Priority` means the same as the
`priority` parameter.

The [Decorators sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Decorators)
shows stacking in action.

## Interceptors

An interceptor implements <xref:Maxen.DI.Interception.IInterceptor>, which has one method. It receives an
<xref:Maxen.DI.Interception.IInvocation> describing the call and decides what to do with it.

```csharp
using Maxen.DI.Interception;

public sealed class CallLogger : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        var args = string.Join(", ", invocation.Arguments);
        Console.WriteLine($"-> {invocation.Method.Name}({args})");

        invocation.Proceed();

        Console.WriteLine($"<- {invocation.ReturnValue}");
    }
}
```

`IInvocation` gives you:

| Member | What it is |
|---|---|
| `Method` | The interface method that was called, as a `MethodInfo`. |
| `Arguments` | The call's arguments. Change items before `Proceed()` and the real method sees the new values. |
| `Target` | The object behind the proxy. |
| `ReturnValue` | The result. Filled in by `Proceed()`; you can read it, replace it, or set it without proceeding. |
| `Proceed()` | Passes the call to the next interceptor, or to the real method if this is the last one. |

If you don't call `Proceed()`, the real method never runs and the caller gets whatever you put in `ReturnValue`. That
is how a cache returns a stored answer. You can also call `Proceed()` more than once, for a retry. Exceptions thrown by
the service reach your interceptor, and the caller, as they were thrown, not wrapped in a reflection exception.

`Intercept` is synchronous. For an async method, `ReturnValue` is the `Task` the method returned, so code after
`Proceed()` runs when the task is handed back, not when it completes.

### Adding an interceptor

To intercept one registration, chain `InterceptWith` onto it:

```csharp
// The interceptor is a service too, so register it.
builder.Register<CallLogger>().AsSingleton();
builder.Register<ICalculator, Calculator>()
    .InterceptWith<CallLogger>();

// Or hand over an instance you created yourself.
builder.Register<IOrders, Orders>()
    .InterceptWith(new CallLogger());
```

`InterceptWith<T>()` takes the interceptor from the container, so it can have dependencies of its own. It is resolved
each time the registration builds a new instance, which for a singleton means once.

To intercept every registration of a service, keyed ones included, without touching each one, use the builder:

```csharp
builder.Register<TimingInterceptor>().AsSingleton();
builder.Intercept<IOrders, TimingInterceptor>();
```

Only interface services can be intercepted. `builder.Intercept` checks this straight away and throws a
`RegistrationException`; `InterceptWith` on a class service fails with a `ResolutionException` when the service is
first resolved.

### Ordering

- On one registration, the first `InterceptWith` runs first and the last one sits nearest the service.
- Interceptors added with `builder.Intercept` run before the registration's own, in the order you added them.
- Interceptors sit outside decorators. A call goes through the interceptors, then the decorators, then the service.

Each interceptor gets its own proxy layer, so in an outer interceptor `Target` is the next proxy in, not the service.

### Generated proxies and the DispatchProxy fallback

An interceptor needs a proxy class that implements the interface. The source generator writes one at compile time for
every interface you intercept in code: the `TService` of `builder.Intercept<TService, ...>()`, and the service of a
generic `Register<IFoo, Foo>()` that `InterceptWith` is chained onto. Generated proxies use no reflection and no
runtime code generation.

The generator skips an interface it can't forward, and falls back to a runtime proxy built on
`System.Reflection.DispatchProxy`. That happens for interfaces with generic methods, `ref`, `out` or `in` parameters,
ref returns, events, indexers or static members, and for interfaces the generator can't see, such as an
`InterceptWith` on a registration stored in a variable. Calls to default interface methods aren't intercepted by a
generated proxy.

Under native AOT the runtime proxy isn't available. If no generated proxy exists, resolving the service throws a
`ResolutionException` that says so. Two more things matter for trimmed and AOT builds:

- `InterceptWith<T>()` and `builder.Intercept<,>()` are marked `[RequiresUnreferencedCode]` and
  `[RequiresDynamicCode]`, so the analyzers warn about them even when a generated proxy exists.
- `InterceptWith(instance)` and the `[Intercept]` attribute carry no such marking.

### The `[Intercept]` attribute

With the source generator you can put interceptors on a `[Service]` class:

```csharp
[Service(ServiceLifetime.Scoped, ServiceType = typeof(IOrders))]
[Intercept(typeof(CallLogger), Order = 0)]
[Intercept(typeof(TimingInterceptor), Order = 1)]
public sealed class Orders : IOrders
{
    // ...
}
```

Lower `Order` runs first. Equal values keep the order the attributes are written in. The service type must be an
interface, so set `ServiceType`, and each interceptor needs a public parameterless constructor because it is created
with `new`. The proxy is generated at compile time. `RegisterAttributed(assembly)` honours the attribute too.

The [Interception sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Interception)
shows both `InterceptWith` and `builder.Intercept`.

## A lighter option: OnActivated and middleware

If all you need is to do something with an instance when it is created, you don't need a wrapper at all.
`OnActivated` runs each time the registration builds a new instance, after decorators have been applied:

```csharp
builder.Register<IReportCache, ReportCache>()
    .AsSingleton()
    .OnActivated((instance, ctx) =>
        ((IReportCache)instance).Warm());
```

For more control, add middleware to a single registration with `.Use(MiddlewarePhase.PostLifetime, (ctx, next) =>
...)`, or to every registration with `builder.Use(...)`. Middleware runs around the resolve, not around method calls.
[Extending](extending.md) explains the pipeline and the two phases, and the
[Middleware sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Middleware) shows
both.

## Decorators or interceptors

Pick a **decorator** when the extra behaviour belongs to one service and you want to write it against that service's
own methods. It is plain, typed code: the compiler checks it, you can step through it, it works for class services
and it costs nothing beyond a normal method call.

Pick an **interceptor** when the same concern applies to many interfaces, such as logging every call or timing every
method, and writing one decorator per interface would mean repeating yourself. The price is that you work with a
`MethodInfo` and an `object?[]` instead of typed parameters, async methods need care, only interfaces qualify, and AOT
builds depend on the generated proxy.

Either way there is a small speed cost. A decorator, or an interceptor added with `builder.Intercept`, switches off
the container's precomputed resolve plans, so every resolve takes the general pipeline. `InterceptWith` and
`OnActivated` only take their own registration, and the services that depend on it, off the fast path. Both paths give
the same results.
