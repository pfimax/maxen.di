# Diagnostics reference

The `Maxen.DI` package ships a source generator and analyzers. Their messages carry an id; this page says what each one
means and how to fix it. All are warnings except `MAXEN003`. To silence one, use `#pragma warning disable MAXEN00x` or set
its severity in `.editorconfig` (`dotnet_diagnostic.MAXEN007.severity = none`).

| Id | Severity | Reported by | Quick fix |
|---|---|---|---|
| [MAXEN001](#maxen001-ambiguous-constructor) | Warning | generator | no |
| [MAXEN002](#maxen002-class-not-supported-by-the-generator) | Warning | generator | no |
| [MAXEN003](#maxen003-class-does-not-implement-its-service-type) | Error | generator | no |
| [MAXEN004](#maxen004-and-maxen005-singleton-depends-on-a-scoped-service) | Warning | generator | yes |
| [MAXEN005](#maxen004-and-maxen005-singleton-depends-on-a-scoped-service) | Warning | analyzer | yes |
| [MAXEN006](#maxen006-constructor-needs-a-service-that-is-not-registered) | Warning | analyzer | yes |
| [MAXEN007](#maxen007-disposable-transient-resolved-from-the-root-container) | Warning | analyzer | yes |
| [MAXEN008](#maxen008-decorator-does-not-take-the-service-it-wraps) | Warning | generator | no |
| [MAXEN009](#maxen009-lazy-or-func-of-a-scoped-service-inside-a-singleton) | Warning | analyzer | no |
| [MAXEN010](#maxen010-scoped-service-resolved-from-the-root-container) | Warning | analyzer | yes |

## MAXEN001: Ambiguous constructor
A `[Service]` class has several accessible constructors (public or internal) with the same, highest number of
parameters, so the generator cannot
tell which one the container would use. Mark the one you want with `[Inject]`, or remove the others.

## MAXEN002: Class not supported by the generator
The class is abstract, static or generic, has optional or by-reference constructor parameters, or uses `[Intercept]`
wrongly (the service is not an interface, or the interceptor has no public parameterless constructor). It is skipped by
`RegisterGeneratedServices()`; register it by hand or with `RegisterAttributed` at runtime.

## MAXEN003: Class does not implement its service type
The `ServiceType` named in `[Service]` (or the type in `[Decorator]`) is not implemented by the class. This is an error
because the registration could never be resolved.

## MAXEN004 and MAXEN005: Singleton depends on a scoped service
A singleton lives for the whole application, so a scoped service it holds would outlive its scope. `MAXEN004` is the
`[Service(ServiceLifetime.Singleton)]` form and `MAXEN005` the `builder.Register<A>().AsSingleton()` form. Make the
singleton scoped or transient, or make the dependency a singleton. The quick fix offers the first two.

`MAXEN005` also follows transients: a singleton that takes a transient that takes a scoped service holds on to that
scoped service just the same, and the message names the transients in between. It only follows a transient with one
public constructor, or one marked `[Inject]`, because otherwise the container chooses the constructor at run time.

## MAXEN006: Constructor needs a service that is not registered
`builder.Register<A>()` is written, but a constructor parameter of `A` is a class or interface that nothing in the project
registers, so resolving `A` would fail. Register the dependency, give the parameter a default value, or mark it
`[FromKey]`. Parameters marked with Microsoft's `[FromKeyedServices]` or `[ServiceKey]` are not checked either. The check
turns itself off when services are registered in ways it cannot follow: scanning, modules, profiles, custom sources,
a service given as a `Type` value (`RegisterFactory(typeof(IFoo), ...)`, `Add(descriptor)`), `Populate`, or a builder
passed to a method in another project. Classes registered through the source generator's `[Service]` attribute count as
registered, so `RegisterGeneratedServices()` does not turn it off.

## MAXEN010: Scoped service resolved from the root container
`container.Resolve<T>()` on the container itself, where `T` is registered as scoped. With scope validation on, which is
the default, that throws a `ScopeValidationException` at run time; a scoped service needs a scope to live in. Create
one (`container.CreateScope()`) and resolve from it, which is what the quick fix does. The warning is not given when
the project sets `ValidateScopes` to anything but `true`, since the root may then hand out scoped services.

### Projects that build more than one container
`MAXEN005`, `MAXEN006` and `MAXEN009` compare registrations with each other, so they need to know which registrations
end up in the same container. A builder created with `var builder = new ContainerBuilder();` and only ever used for
calls on it is a container the analyzer can see all of. It is checked against its own registrations only, and a call
the analyzer can't follow on another builder doesn't switch its checks off. Every other registration (made on a
parameter, a lambda parameter, a field, or a builder that was passed to a method, returned or assigned) could belong
to any container, so those are checked together against everything the project registers, and `MAXEN006` is off for
them as soon as the project has one call it can't follow.

## MAXEN007: Disposable transient resolved from the root container
Resolving a disposable transient straight from the root container keeps every instance alive until the container itself is
disposed, which is a memory leak in long-running programs. Resolve it from a scope (`container.CreateScope()`) so the
scope disposes it when it ends.

## MAXEN008: Decorator does not take the service it wraps
A `[Decorator(typeof(IFoo))]` class has no constructor parameter of type `IFoo`, so there is nothing to wrap and it cannot
be built. Add an `IFoo inner` parameter.

## MAXEN009: Lazy or Func of a scoped service inside a singleton
A singleton that takes `Lazy<T>` or `Func<T>` of a scoped service looks safe, but the delegate remembers the scope it was
created in, so the scoped service is resolved from a scope that may already be gone. Take the
`IContainer` instead and create a scope for each use (`container.CreateScope()`), or make the service a singleton.
