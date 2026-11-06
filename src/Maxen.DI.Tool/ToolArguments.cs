namespace Maxen.DI.Tool;

/// <summary>What the user asked for on the command line, or a message explaining what was wrong with it.</summary>
internal sealed class ToolArguments
{
    public const string Usage = """
        Usage:
          maxen-di graph    <assembly.dll> [--format mermaid|dot|text|json] [--profile <name>]... [--validate]
          maxen-di validate <assembly.dll> [--profile <name>]...
          maxen-di diff     <old.dll> <new.dll> [--profile <name>]...
          maxen-di diff     <assembly.dll> --baseline <snapshot.json> [--profile <name>]...
          maxen-di snapshot <assembly.dll> <snapshot.json> [--profile <name>]...
          maxen-di explain  <assembly.dll> <service type> [--profile <name>]...
          maxen-di init     <ModuleName> [--namespace <ns>] [--directory <dir>]

          graph       Builds a container from the IContainerModule classes in the assembly and prints who depends on whom.
          validate    Checks for missing dependencies, loops and lifetime mismatches and prints nothing else; exits with 1 if any.
          diff        Compares the graphs of two builds (or a build and a saved snapshot); exits with 1 if anything changed.
          snapshot    Saves the graph as JSON, to commit as a baseline for 'diff --baseline'.
          explain     Lists every registration of a service (which one wins, which are conditional) and what it needs and who needs it.
          init        Writes a starter IContainerModule class.
          --format    mermaid (default), dot, text or json (graph only).
          --profile   Switches a profile on before building. May be repeated.
          --validate  With graph: also validate after printing.
        """;

    private static readonly string[] Commands = { "graph", "validate", "diff", "explain", "snapshot", "init" };

    public string Command { get; private init; } = string.Empty;
    public string AssemblyPath { get; private init; } = string.Empty;

    /// <summary>The second assembly of a <c>diff</c>.</summary>
    public string OtherAssemblyPath { get; private init; } = string.Empty;

    /// <summary>The saved snapshot a <c>diff</c> compares against instead of a second assembly, or empty.</summary>
    public string BaselinePath { get; private init; } = string.Empty;

    /// <summary>Where <c>snapshot</c> writes its JSON.</summary>
    public string OutputPath { get; private init; } = string.Empty;

    /// <summary>The service an <c>explain</c> is about, as a simple or full type name.</summary>
    public string ServiceName { get; private init; } = string.Empty;

    /// <summary>The module class <c>init</c> writes.</summary>
    public string ModuleName { get; private init; } = string.Empty;

    /// <summary>Namespace for the module <c>init</c> writes.</summary>
    public string Namespace { get; private init; } = "MyApp";

    /// <summary>Folder <c>init</c> writes into.</summary>
    public string Directory { get; private init; } = ".";

    public string Format { get; private init; } = "mermaid";
    public bool Validate { get; private init; }
    public IReadOnlyList<string> Profiles { get; private init; } = Array.Empty<string>();

    /// <summary>Null when the arguments are fine; otherwise what to tell the user.</summary>
    public string? Error { get; private init; }

    public static ToolArguments Parse(string[] args)
    {
        if (args.Length == 0 || !Commands.Contains(args[0]))
            return new ToolArguments { Error = $"Expected a command: {string.Join(", ", Commands)}." };

        var command = args[0];

        // How many values come right after the command, before any --option. A diff against a snapshot needs only one.
        var positional = command switch
        {
            "diff" => args.Contains("--baseline") ? 1 : 2,
            "explain" or "snapshot" => 2,
            _ => 1,
        };
        if (args.Length < 1 + positional)
            return new ToolArguments { Error = $"'{command}' is missing arguments." };

        var format = "mermaid";
        var validate = command == "validate";
        var profiles = new List<string>();
        var baseline = string.Empty;
        var ns = "MyApp";
        var directory = ".";

        for (var i = 1 + positional; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--validate" when command == "graph":
                    validate = true;
                    break;
                case "--format" when i + 1 < args.Length && command == "graph":
                    format = args[++i].ToLowerInvariant();
                    break;
                case "--profile" when i + 1 < args.Length && command != "init":
                    profiles.Add(args[++i]);
                    break;
                case "--baseline" when i + 1 < args.Length && command == "diff":
                    baseline = args[++i];
                    break;
                case "--namespace" when i + 1 < args.Length && command == "init":
                    ns = args[++i];
                    break;
                case "--directory" when i + 1 < args.Length && command == "init":
                    directory = args[++i];
                    break;
                default:
                    return new ToolArguments { Error = $"Unknown or incomplete option '{args[i]}' for '{command}'." };
            }
        }

        if (format is not ("mermaid" or "dot" or "text" or "json"))
            return new ToolArguments { Error = $"Unknown format '{format}'. Use mermaid, dot, text or json." };

        return new ToolArguments
        {
            Command = command,
            AssemblyPath = command == "init" ? string.Empty : args[1],
            OtherAssemblyPath = command == "diff" && baseline.Length == 0 ? args[2] : string.Empty,
            BaselinePath = baseline,
            OutputPath = command == "snapshot" ? args[2] : string.Empty,
            ServiceName = command == "explain" ? args[2] : string.Empty,
            ModuleName = command == "init" ? args[1] : string.Empty,
            Namespace = ns,
            Directory = directory,
            Format = format,
            Validate = validate,
            Profiles = profiles,
        };
    }
}
