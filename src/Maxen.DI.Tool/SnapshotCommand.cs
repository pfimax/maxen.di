namespace Maxen.DI.Tool;

/// <summary>Saves an assembly's graph as JSON, so a later build can be compared with it without keeping the old DLL around.</summary>
internal static class SnapshotCommand
{
    public static int Run(ToolArguments arguments, TextWriter output, TextWriter error)
        => ModuleInspector.Inspect(arguments.AssemblyPath, arguments.Profiles, error, container =>
        {
            var json = GraphSnapshot.Of(container.BuildGraph()).ToJson();
            File.WriteAllText(arguments.OutputPath, json + Environment.NewLine);
            output.WriteLine($"Wrote {Path.GetFullPath(arguments.OutputPath)}");
            return 0;
        });
}
