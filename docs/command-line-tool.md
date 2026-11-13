# The command line tool

`maxen-di` looks at the registrations in a built assembly without running your application. It prints the dependency
graph, checks it for missing services, loops and lifetime mistakes, compares two builds, and explains how one service
is put together. Its exit codes make it usable as a gate in CI.

## Installing

The tool is a .NET global tool, published as `Maxen.DI.Tool`:

```bash
dotnet tool install -g Maxen.DI.Tool
```

The command it installs is `maxen-di`. Run it without arguments to see the usage text.

## How it works

You point the tool at a compiled assembly, usually from `bin/`. It then:

1. loads the assembly in a load context of its own, together with the dependencies listed in its `.deps.json`;
2. finds every class that implements <xref:Maxen.DI.Extensibility.IContainerModule> and has a public parameterless
   constructor, and applies each one to a fresh `ContainerBuilder`;
3. switches on any profiles you name with `--profile`;
4. builds the container and runs the command against it;
5. unloads the assembly again.

Point it at the build output folder rather than `obj/`, so the `.deps.json` and the referenced assemblies are next to
the DLL. The tool uses its own copy of Maxen.DI instead of the one in your output folder, so keep the tool's version in
step with the `Maxen.DI` package your project uses. When they differ, the tool says so on standard error, with the
command that installs the matching version, and carries on.

### What the tool can't see

The tool only knows what your modules register. It never runs `Program.cs`, so registrations made anywhere else are
invisible to it: calls on `ContainerBuilder` in your startup code, `AddSingleton` and friends on `IServiceCollection`,
and services the host adds for you. If no module registers anything, the tool stops with
`No registrations found` and exit code 2.

To give the tool the full picture, move your registrations into a module and apply that module from your startup code:

```csharp
public sealed class BillingModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        builder.Register<IInvoiceStore, SqlInvoiceStore>().AsScoped();
        builder.Register<IBillingService, BillingService>().AsScoped();
    }
}

// Program.cs
builder.UseMaxenDI(c => c.RegisterModule(new BillingModule()));
```

`maxen-di init` writes the skeleton of such a module for you.

Services the host provides, such as `ILogger<T>`, still aren't in the module graph, so `validate` reports them as
missing. One way round it is a profile, defined in a module, that registers stand-ins only when the tool asks for it:

```csharp
builder.DefineProfile("Inspection", p =>
    p.Register(typeof(ILogger<>), typeof(NullLogger<>)));
```

Then run the tool with `--profile Inspection`.

One more thing the graph can't show: the dependencies of factory and instance registrations, because the tool can't
look inside a factory. The exception is a factory that says what it resolves (`ServiceMetadata.Dependencies`), which
the registrations the [source generator](source-generator.md) writes for `[Service]` classes all do.

### Profiles

`--profile <name>` switches on a profile defined with `DefineProfile` in one of your modules, the same as
`builder.UseProfile(name)`. Repeat it to switch on several. It works with every command except `init`. A profile that
no module defines stops the tool with exit code 2 and a list of the profiles that do exist. Profiles are covered in
[Modules, profiles and options](configuration.md).

## Commands

Options always come after the positional arguments.

### `graph`

```bash
maxen-di graph <assembly.dll> [--format mermaid|dot|text|json]
    [--profile <name>]... [--validate]
```

Prints who depends on whom. The format is one of:

| Format | Output |
|---|---|
| `mermaid` | A Mermaid diagram, which GitHub renders in Markdown. The default. |
| `dot` | Graphviz DOT, for `dot -Tsvg` and similar. |
| `text` | Plain text, for reading in a terminal. |
| `json` | An object with a `nodes` array (one line per registration) and an `edges` array (one line per dependency). |

With `--validate`, the graph is printed first, then any problems are written to standard error and the exit code is 1.

### `validate`

```bash
maxen-di validate <assembly.dll> [--profile <name>]...
```

Checks for missing dependencies, dependency loops and lifetime mismatches, such as a singleton that takes a scoped
service. It doesn't print the graph. A healthy assembly prints nothing and exits with 0; each problem is written to
standard error and the exit code is 1. This is the command for a CI gate.

### `diff`

```bash
maxen-di diff <old.dll> <new.dll> [--profile <name>]...
maxen-di diff <assembly.dll> --baseline <snapshot.json>
    [--profile <name>]...
```

Compares two graphs: either two builds of the assembly, or one build and a snapshot saved earlier with `snapshot`
(the snapshot is the "before"). Each change is one line, sorted, starting with `- registration:`,
`+ registration:`, `- dependency:` or `+ dependency:`. A registration line holds the service, what builds it and its
lifetime, so changing a lifetime shows as one removed and one added registration.

With no changes it prints `No differences.` and exits with 0. Any change gives exit code 1, which makes a pull request
that changes the graph fail until the snapshot is updated.

### `snapshot`

```bash
maxen-di snapshot <assembly.dll> <snapshot.json> [--profile <name>]...
```

Saves the graph as JSON, in the same shape as `graph --format json`, and prints the path it wrote. Entries are sorted,
so two runs over the same build give identical files and the file diffs cleanly in source control. Commit it and
compare later builds against it with `diff --baseline`.

### `explain`

```bash
maxen-di explain <assembly.dll> <service type> [--profile <name>]...
```

Answers "what happens when someone asks for this service?". The service type can be the simple name (`IClock`) or the
full name with its namespace. For a generic type, use the .NET name with the backtick, such as ``IRepo`1``, which
matches every closed form in the graph.

For each matching service, the tool prints every registration in registration order. The last is marked
`wins unless a condition rejects it`, and registrations with a condition are marked `conditional`. After that come two
lines: `needs:`, what the winning registration depends on (`(deferred)` for `Lazy<T>` and `Func<T>`), and
`needed by:`, the services that depend on it. If no registration matches, it says so on standard error and exits
with 1.

### `init`

```bash
maxen-di init <ModuleName> [--namespace <ns>] [--directory <dir>]
```

Writes a starter module class. `Module` is added to the name unless it already ends with it, so
`maxen-di init Billing --namespace Shop` writes `BillingModule.cs` with a public sealed `BillingModule` in namespace
`Shop`. The namespace defaults to `MyApp` and the directory to the current one, which is created if it doesn't exist.
`init` never overwrites a file: if `BillingModule.cs` is already there, it exits with 1. A name or namespace that isn't
a valid C# identifier gives exit code 2.

## Using it as a CI gate

Build first, because the tool reads the compiled assembly. Then run `validate` to catch broken graphs, and `diff`
against a snapshot you keep in the repository to make every change to the registrations visible in review.

Create the snapshot once and commit it:

```bash
dotnet build -c Release
maxen-di snapshot bin/Release/net10.0/MyApp.dll di-graph.json
```

Then add a job to your workflow:

```yaml
name: Container checks

on: [push, pull_request]

jobs:
  di:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x

      - run: dotnet build src/MyApp -c Release
      - run: dotnet tool install -g Maxen.DI.Tool

      - name: Validate the registrations
        run: maxen-di validate src/MyApp/bin/Release/net10.0/MyApp.dll

      - name: Compare with the committed graph
        run: >
          maxen-di diff src/MyApp/bin/Release/net10.0/MyApp.dll
          --baseline di-graph.json
```

When a pull request changes the graph on purpose, the `diff` step fails and lists the changes. Run `snapshot` again
and commit the new file with the change, so the reviewer sees the graph change next to the code that caused it.

The [samples README](https://github.com/pfimax/maxen.di/tree/main/samples) has commands to try the tool on the feature
tour, whose modules are in
[Maxen.DI.Samples.Features/Modules](https://github.com/pfimax/maxen.di/tree/main/samples/Maxen.DI.Samples.Features/Modules).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success: the graph or explanation was printed, validation found nothing, `diff` found no differences, or the file was written. |
| 1 | The answer is "no": `validate` or `graph --validate` found problems, `diff` found differences, `explain` found no registration, or `init` found the file already there. |
| 2 | The tool couldn't do its job: bad arguments (the usage text is printed), the assembly or snapshot wasn't found, the snapshot isn't valid JSON or isn't shaped like one, no module registered anything, the assembly or one of its dependencies couldn't be loaded, a module threw, building the container failed (an undefined profile, for example), or `init` was given an invalid name. |

When the inspected app's own code fails, for example a module whose `Configure` throws or a dependency missing from
the build output, the tool prints `Could not inspect` with that error's message and exits with 2. For other ways to
check the graph, at startup or in tests, see [Validation and diagnostics](validation-and-diagnostics.md).
