# Maxen.DI.Tool

[![NuGet](https://img.shields.io/nuget/v/Maxen.DI.Tool?label=NuGet&color=067a4f)](https://www.nuget.org/packages/Maxen.DI.Tool) ![.NET 8, 9, 10](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512bd4) [![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/pfimax/maxen.di/blob/main/LICENSE)

`maxen-di` looks at the [Maxen.DI](https://www.nuget.org/packages/Maxen.DI) registrations in a built assembly without
running your application. It prints the dependency graph, checks it for missing services, loops and lifetime mistakes,
compares two builds, and explains how one service is put together. Its exit codes make it a gate in CI.

## Install

```bash
dotnet tool install -g Maxen.DI.Tool
```

## Commands

```bash
maxen-di validate bin/Release/net10.0/MyApp.dll
maxen-di graph    bin/Release/net10.0/MyApp.dll --format mermaid
maxen-di explain  bin/Release/net10.0/MyApp.dll IGreeter
maxen-di snapshot bin/Release/net10.0/MyApp.dll di-graph.json
maxen-di diff     bin/Release/net10.0/MyApp.dll --baseline di-graph.json
maxen-di init     Billing --namespace Shop
```

| Command | What it does |
|---|---|
| `validate` | Checks for missing dependencies, loops and lifetime mismatches. Silent when healthy; exits with 1 on any problem. |
| `graph` | Prints who depends on whom as `mermaid`, `dot`, `text` or `json`. Add `--validate` to check as well. |
| `explain` | Lists every registration of a service, which one wins, what it needs and what needs it. |
| `snapshot` | Saves the graph as JSON, to commit as a baseline. |
| `diff` | Compares two builds, or a build and a snapshot. Exits with 1 if a registration or dependency changed. |
| `init` | Writes a starter module class. |

Every command except `init` takes `--profile <name>`, repeatable, to switch on profiles defined in your modules.

## What it sees

The tool builds a container from the public `IContainerModule` classes in your assembly. It never runs `Program.cs`,
so registrations made elsewhere, such as directly on `IServiceCollection`, are invisible to it. Put the registrations
you want checked in a module and apply that module from your startup code:

```csharp
public sealed class BillingModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        builder.Register<IInvoiceStore, SqlInvoiceStore>().AsScoped();
        builder.Register<IBillingService, BillingService>();
    }
}

// Program.cs
builder.UseMaxenDI(c => c.RegisterModule(new BillingModule()));
```

## In CI

```yaml
- run: dotnet build src/MyApp -c Release
- run: dotnet tool install -g Maxen.DI.Tool
- run: maxen-di validate src/MyApp/bin/Release/net10.0/MyApp.dll
- run: >
    maxen-di diff src/MyApp/bin/Release/net10.0/MyApp.dll
    --baseline di-graph.json
```

When a pull request changes the graph on purpose, run `snapshot` again and commit the new file with the change.

Exit codes: 0 for success, 1 when the answer is "no" (problems found, the graph changed, nothing matched), and 2 when
the tool couldn't do its job (bad arguments, a missing file, or the assembly's own code failing to load).

## Learn more

- [The command line tool](https://pfimax.github.io/maxen.di/docs/command-line-tool.html): the full guide
- [Documentation](https://pfimax.github.io/maxen.di/) · [Changelog](https://github.com/pfimax/maxen.di/blob/main/CHANGELOG.md)
