# Extending Maxen.DI

Everything the container does by default is built from the same pieces you can use yourself.

## How a resolve flows

```
Validate (loops, captive dependencies)
 -> pre-lifetime middleware      runs on every resolve
 -> lifetime                     decides whether to reuse or build
 -> post-lifetime middleware     runs only when something new is built
 -> decorators                   first registered sits closest to the service (see `priority`)
 -> activation                   constructor, then initializers
```

A singleton that already exists skips all of this, unless its registration has pre-lifetime middleware.

## Writing a lifetime

```csharp
public sealed class PerTenantLifetime : Lifetime
{
    public override string Name => "PerTenant";
    public override bool IsScopeBound => true;   // longer-lived services must not capture it

    public override object Resolve(ResolveContext context, ResolveDelegate next)
        => context.Scope.Instances.GetOrCreate(
            (context.Descriptor, TenantId(context)), () => next(context), context.Descriptor.OwnsInstance);
}

builder.Register<IReport, Report>().WithLifetime(new PerTenantLifetime());
```

`InstanceStore` is what scopes use to cache instances and to dispose them (newest first) when the scope ends.

## Middleware

```csharp
builder.Use(MiddlewarePhase.PreLifetime, (ctx, next) =>
{
    Console.WriteLine($"resolving {ctx.Key}");
    return next(ctx);
});
```

`PostLifetime` middleware only sees newly built instances, so it is the place to wrap or audit creation.

## The other extension points

| Point | Interface | Added with |
|---|---|---|
| On-the-fly registrations (like `Lazy<T>`) | `IRegistrationSource` | `AddSource` |
| Picking a constructor | `IConstructorSelector` | `ConstructorSelector = ...` |
| Filling constructor parameters | `IParameterResolver` | `AddParameterResolver` |
| Post-construction steps | `IInstanceInitializer` | `AddInitializer` |
| Method interception | `IInterceptor` | `.InterceptWith<T>()` |
| Watching resolves | `IResolutionObserver` | `Observe` |
| Reusable setup | `IContainerModule` | `RegisterModule` |

Adding a custom `IParameterResolver` switches the container from its fastest construction path to the general
one, because your resolver might claim any parameter. That costs some speed per construction; it is still correct.

## Trimming and native AOT

The core library is analyzed for trimming and AOT (`IsAotCompatible`). Registrations made with
`Register<T, TImpl>()`, factories, instances and the `[Service]` source generator are safe. Features that
inherently use reflection are marked `[RequiresUnreferencedCode]` so you are warned when you use them:
assembly scanning, `[Inject]` properties and methods, interception, and binding options from dictionaries or
configuration. Open-generic registrations work for closed types your app actually uses.
The console sample publishes cleanly with the IL trimmer and runs. It is also set up for `PublishAot`; the native
compile step passes with no warnings, but a finished native executable has not been run yet. The README has the exact status.

The source generator also writes constructor calls for the types you register in code and stores them in
`GeneratedActivators`. The container prefers them over compiled expression trees, which matters most on native AOT.
Write your own source of generated activators the same way if you build types some other way:

```csharp
GeneratedActivators.Add(typeof(Foo), new[] { typeof(IClock) }, static args => new Foo((IClock)args[0]!));
```
