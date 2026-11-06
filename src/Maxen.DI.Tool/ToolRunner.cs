namespace Maxen.DI.Tool;

/// <summary>
/// Everything <c>Program.cs</c> does, as a method the tests can call with their own writers instead of the console.
/// </summary>
internal static class ToolRunner
{
    /// <summary>Parses <paramref name="args"/>, runs the command and returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var arguments = ToolArguments.Parse(args);
        if (arguments.Error is not null)
        {
            // The usage text is printed here and only here, whatever went wrong with the arguments.
            error.WriteLine(arguments.Error);
            error.WriteLine();
            error.WriteLine(ToolArguments.Usage);
            return 2;
        }

        return arguments.Command switch
        {
            "diff" => DiffCommand.Run(arguments, output, error),
            "explain" => ExplainCommand.Run(arguments, output, error),
            "snapshot" => SnapshotCommand.Run(arguments, output, error),
            "init" => InitCommand.Run(arguments, output, error),
            _ => GraphCommand.Run(arguments, output, error),
        };
    }
}
