# Generator sample

Run it:

```bash
dotnet run --project samples/Maxen.DI.Samples.Generators
```

What the source generator does for you here:

| Attribute / call | Result at compile time |
|---|---|
| `[Service(ServiceLifetime.X, ServiceType = ..., Key = ...)]` | a registration, applied by `builder.RegisterGeneratedServices()` |
| `[Decorator(typeof(IFoo), Priority = n)]` | the decorator is wired up, open generics included |
| `[Intercept(typeof(MyInterceptor), Order = n)]` | a proxy class that calls the interceptors, no run-time emit |
| `builder.Register<Plain>()` | a direct constructor call stored in `GeneratedActivators` |

The generated code lives in the namespace named after your assembly (here `Maxen.DI.Samples.Generators`), so add a
`using` for it, as `Program.cs` does. The method is `internal`: each assembly registers its own `[Service]` classes. To read the generated files, uncomment the
`EmitCompilerGeneratedFiles` lines in the project file and look in `obj/generated`.

## Warnings the analyzers can give you

Paste any of these into `Program.cs` to see the diagnostic (they are warnings, except MAXEN003 which is an error). The
full list with fixes is in [docs/diagnostics.md](../../docs/diagnostics.md).

```csharp
builder.Register<ReportService>().AsSingleton();          // MAXEN005 if ReportService takes a scoped service
builder.Register<Needs>();                                // MAXEN006 if Needs takes a service nobody registers
container.Resolve<DisposableTransient>();                 // MAXEN007 when resolved from the root container
container.Resolve<ScopedService>();                       // MAXEN010: a scoped service needs a scope
[Service(ServiceType = typeof(IFoo))] class NotAFoo { }   // MAXEN003: does not implement IFoo (error)
```

MAXEN004-007 and MAXEN010 come with code fixes in Visual Studio and Rider (`Maxen.DI.CodeFixes` ships inside the package).
