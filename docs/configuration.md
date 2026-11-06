# Modules, profiles and options

As an application grows, its container setup needs structure. This page covers the tools for that: modules to group
registrations, profiles and feature flags to switch groups on and off, options for settings objects, assembly scanning
to register classes by convention, child containers for overrides, and property and method injection for the cases
constructors don't suit.

## Modules

A module is a class that implements <xref:Maxen.DI.Extensibility.IContainerModule>. Its one method receives the
builder, so a module can do anything you would do in `Program.cs`: register services, add decorators, plug in
middleware.

```csharp
using Maxen.DI.Extensibility;

public sealed class BillingModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        builder.Register<IInvoiceStore, SqlInvoiceStore>().AsScoped();
        builder.Register<IBillingService, BillingService>();
        builder.Decorate<IBillingService, AuditedBilling>();
    }
}
```

Apply it in one of three ways:

```csharp
// Creates the module with its parameterless constructor.
builder.RegisterModule<BillingModule>();

// Uses an instance, so the module can take settings.
builder.RegisterModule(new StorageModule(connectionString));

// Applies every module in the assembly.
builder.RegisterModules(typeof(Program).Assembly);
```

`RegisterModule` runs the module's `Configure` straight away, so its registrations land where the call is.
`RegisterModules` applies every non-abstract `IContainerModule` with a public parameterless constructor, in the order
reflection lists the types, so don't rely on an order between modules found that way. It reads types by reflection
and is marked `[RequiresUnreferencedCode]`.

Modules are also what the [command line tool](command-line-tool.md) reads: `maxen-di validate` builds a container
from the `IContainerModule` classes in your assembly, and `maxen-di init` writes a starter module for you.

## Profiles

A profile is a named set of registrations that only applies when you switch it on. The typical use is swapping real
services for fakes in development, or for different settings per environment.

```csharp
builder.Register<IMailer, SmtpMailer>();

builder.DefineProfile("Development", p =>
{
    p.Replace<IMailer, ConsoleMailer>();
    p.Register<IPayments, FakePayments>();
});

builder.UseProfile("Development");
```

The callback receives the same builder, so everything you can do on a builder works inside a profile.

Profiles are applied during `Build()`, after everything registered directly. That is why a profile wins over the
defaults: its registrations come last, and the last registration wins. With several active profiles, they apply in the
order you switched them on. Profile names ignore case, and defining the same name twice adds to the profile rather than
replacing it.

To choose the profile without a code change, read it from an environment variable:

```csharp
// Reads MAXEN_PROFILE, for example MAXEN_PROFILE=Development.
builder.UseProfileFromEnvironment();

// Or a variable of your own.
builder.UseProfileFromEnvironment("APP_PROFILE");
```

If the variable isn't set, nothing happens. Its whole value is taken as one profile name.

Switching on a profile that was never defined makes `Build()` throw a `RegistrationException` that lists the profiles
that were defined. A typo in an environment variable therefore stops the app at startup rather than quietly running
with the defaults.

The [Profiles sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Profiles)
shows this end to end.

## Feature flags

A feature flag puts a module behind a name. The module is only applied when that feature is switched on.

```csharp
builder.RegisterModule<ReportingModule>();
builder.RegisterModule<BetaReportsModule>("beta");

builder.UseFeatures("beta");

// Or from MAXEN_FEATURES, for example MAXEN_FEATURES=beta,exports
builder.UseFeaturesFromEnvironment();
```

`UseFeaturesFromEnvironment` reads `MAXEN_FEATURES` unless you pass another variable name, and splits the value on
commas or semicolons. If the variable isn't set, nothing changes.

A feature is a profile under another name. `RegisterModule<T>("beta")` defines a profile called `beta`
that applies the module, and `UseFeatures("beta")` is `UseProfile("beta")`. So everything said about profiles holds:
feature modules apply at `Build()` and win over the defaults, the same module can be listed under several features,
features and profiles share one set of names, and switching on a feature nothing was registered under fails the build
with an error that calls it a profile.

The [Modules sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Modules) shows a
module behind a feature flag.

## Options

Options give you a typed settings object instead of loose strings. Add configuration steps for a class, then inject
`IOptions<T>` where you need the result.

```csharp
using Maxen.DI.Options;

public sealed class MailSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
}

builder.Configure<MailSettings>(s => s.Host = "smtp.example.com");
builder.Configure<MailSettings>(s => s.Port = 587);

public sealed class Mailer(IOptions<MailSettings> options)
{
    private readonly MailSettings _settings = options.Value;
}
```

The settings class needs a public parameterless constructor. You can call `Configure<T>` as often as you like, and the
steps run in the order you added them. `IOptions<T>` is a singleton: the object is created and every step runs the
first time `IOptions<T>` is resolved, and the result is kept for the life of the container. Nothing reloads it later.

`IOptions<T>` here is `Maxen.DI.Options.IOptions<T>`, not the one from `Microsoft.Extensions.Options`. If you run
Maxen.DI behind `IServiceCollection`, Microsoft's `services.Configure<T>()` and its `IOptions<T>` keep working as
before; the two don't share configuration steps.

### Binding from key/value pairs

`BindOptions<T>` fills properties from flat `"Section:Name"` pairs, which suits environment variables, command lines,
or anything shaped like a configuration file.

```csharp
var values = new Dictionary<string, string?>
{
    ["Mail:Host"] = "smtp.example.com",
    ["Mail:Port"] = "587",
    ["Mail:Retry:Count"] = "3",
};

builder.BindOptions<MailSettings>(values, section: "Mail");
```

Keys match property names without regard to case. Nested classes bind from `Parent:Child` keys. Strings, numbers,
`bool`, enums, `TimeSpan`, `Guid` and `Uri` are converted; collections are not supported. Missing keys leave the
default value alone. A value that can't be converted fails with an error naming the setting, when `IOptions<T>` is
first resolved. Binding is just another configuration step, so a `Configure<T>` added later can still adjust the
result. It uses reflection and is marked `[RequiresUnreferencedCode]`.

### Binding from IConfiguration

The `Maxen.DI.Extensions.DependencyInjection` package adds an overload that takes an `IConfiguration`, such as a
section of `appsettings.json`:

```csharp
using Maxen.DI.Extensions.DependencyInjection;

builder.UseMaxenDI(c => c.BindOptions<MailSettings>(
    builder.Configuration.GetSection("Mail")));
```

It uses Microsoft's configuration binder, and like the dictionary version it reads the values when `IOptions<T>` is
first resolved and works alongside `Configure<T>` steps. It is marked `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`.

The [Options sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Options) has a
runnable version.

## Assembly scanning

When you have many classes that follow a convention, you can register them by rule instead of one by one.

```csharp
using Maxen.DI.Scanning;

builder.Scan(typeof(Program).Assembly, s => s
    .InNamespace("Shop.Services")
    .NameEndsWith("Service")
    .AsImplementedInterfaces()
    .AsScoped());
```

| Method | What it does |
|---|---|
| `Where(predicate)` | Keeps classes the predicate accepts. Each call narrows further. |
| `InNamespace(ns)` | Keeps classes in that namespace or one below it. |
| `AssignableTo<T>()`, `AssignableTo(type)` | Keeps classes that implement or derive from the type. An open generic such as `typeof(IHandler<>)` matches every closed form. |
| `NameEndsWith(suffix)` | Keeps classes whose name ends with the text (case-sensitive). |
| `IncludeNonPublicTypes()` | Includes internal and nested private classes, which are skipped by default. |
| `AsSelf()` | Registers each class as itself. |
| `AsImplementedInterfaces()` | Registers each class as its public interfaces, leaving out `System.*` ones such as `IDisposable`. |
| `As(selector)` | Registers each class as the types your function returns. Combines with the other `As` methods. |
| `AsTransient()`, `AsScoped()`, `AsSingleton()`, `WithLifetime(lifetime)` | Sets the lifetime. Transient by default. |

Without an `As` call, each class is registered as itself. Abstract and compiler-generated classes are always skipped.
A class registered under several service types gets one registration with the others added through `AlsoAs`, so a
singleton stays a single object whichever interface you ask for. An open generic class such as `Repo<T>` is
registered as an open generic for each generic interface it implements.

Each `Scan` call is one rule, so call it again for a different group of classes. There is also an overload that takes
several assemblies, and the older `RegisterAssemblyTypes(assembly, filter, lifetime)` shortcut still works.

### Attributes

If you'd rather mark classes than write rules, put `[Service]` on them and call `RegisterAttributed`:

```csharp
[Service(ServiceLifetime.Scoped, ServiceType = typeof(IOrders))]
public sealed class Orders : IOrders
{
    // ...
}

builder.RegisterAttributed(typeof(Program).Assembly);
```

`ServiceType` defaults to the class itself, and `Key` makes a keyed registration. `RegisterAttributed` also applies
`[Decorator]` and `[Intercept]` attributes; see [Decorators and interception](decorators-and-interception.md).

### Trimming

`Scan`, `RegisterAttributed`, `RegisterModules` and `RegisterAssemblyTypes` all find types by reflection. They are
marked `[RequiresUnreferencedCode]`, because the trimmer may remove a class that nothing else references. For trimmed
or native AOT apps, register services in code, or let the [source generator](source-generator.md) turn your
`[Service]` classes into plain registrations with `builder.RegisterGeneratedServices()`.

The [Scanning sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Scanning) uses
both styles.

## Child containers

A child container starts from an existing container and adds or overrides registrations, without touching the parent.

```csharp
using var tenant = container.CreateChild(c =>
    c.Register<IMessageSender, SmsSender>().AsSingleton());

// SmsSender in the child, still the original in the parent.
var sender = tenant.Resolve<IMessageSender>();
```

What a child does:

- Anything it doesn't register is looked up in the parent. Its own registrations win.
- `ResolveAll` returns the parent's registrations first, then the child's.
- Singletons that belong to the parent are shared, so the child hands out the same instances.
- It inherits the parent's middleware, decorators, registration sources, parameter resolvers, initializers and
  constructor selector. Of the options, `ValidateScopes` and `ValidateOnBuild` are copied. Profiles are not inherited.

A service the parent registers is still built by the parent, with the parent's registrations. If the child replaces
`IClock`, a parent-registered service that takes an `IClock` keeps getting the parent's one, and decorators or
middleware you add in the child only apply to the child's own registrations.

The child owns and disposes what it creates: its own singletons, and the scoped and transient services resolved
through it. Disposing the child leaves the parent's singletons alone. The parent doesn't track its children, so
dispose each child yourself.

Child containers suit tests that swap one or two services on top of the real setup, per-tenant overrides, and plugins
that bring their own registrations.

The [ChildContainers sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/ChildContainers)
shows a per-tenant override.

## Property and method injection

Constructors are the main way services get their dependencies, and Maxen.DI keeps it that way by default. When a
constructor doesn't suit, for example with a framework that creates the object for you or for an optional extra, you
can switch on property injection, method injection, or both.

```csharp
builder.UsePropertyInjection();
builder.UseMethodInjection();

public sealed class Component
{
    [Inject]
    public Settings? Settings { get; set; }

    [Inject(Required = false)]
    public IAuditLog? Audit { get; set; }

    [Inject]
    public void Initialize(IClock clock, [FromKey("eu")] IRates rates)
    {
        // Called after the constructor, with resolved services.
    }
}
```

With property injection on, public properties with a public setter and `[Inject]` are filled after construction. A
missing service throws, unless you set `Required = false`, in which case the property is left alone. Add `[FromKey]`
to a property to ask for a keyed service.

With method injection on, public instance methods marked `[Inject]` are called after construction. Their parameters
are resolved the same way as constructor parameters, `[FromKey]` and default values included. If the method throws,
you get its own exception.

Both run on instances the container builds, from a constructor or a factory, but not on objects you registered with
`RegisterInstance`. They run on the real object, before any decorator wraps it. Validation and the dependency graph
read constructors only, so a missing service for a property or method shows up when the service is resolved.

Both features read members by reflection and are marked `[RequiresUnreferencedCode]`. Switching either on also turns
off the container's precomputed resolve plans, which makes resolves a little slower.

The [Injection sample](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Injection)
uses both.
