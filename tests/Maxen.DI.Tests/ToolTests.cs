using Maxen.DI.Tool;

namespace Maxen.DI.Tests;

public class ToolTests
{
    // The test assembly itself is a handy target: it holds a real IContainerModule (PerThreadClockModule).
    private static readonly string ThisAssembly = typeof(ToolTests).Assembly.Location;

    [Fact]
    public void Arguments_are_parsed_with_sensible_defaults()
    {
        var parsed = ToolArguments.Parse(new[] { "graph", "app.dll" });

        Assert.Null(parsed.Error);
        Assert.Equal("app.dll", parsed.AssemblyPath);
        Assert.Equal("mermaid", parsed.Format);
        Assert.False(parsed.Validate);
    }

    [Fact]
    public void Options_are_read()
    {
        var parsed = ToolArguments.Parse(new[] { "graph", "app.dll", "--format", "DOT", "--profile", "Dev", "--profile", "Test", "--validate" });

        Assert.Null(parsed.Error);
        Assert.Equal("dot", parsed.Format);
        Assert.True(parsed.Validate);
        Assert.Equal(new[] { "Dev", "Test" }, parsed.Profiles);
    }

    [Theory]
    [InlineData("graph")]
    [InlineData("nonsense", "app.dll")]
    [InlineData("graph", "app.dll", "--format", "pdf")]
    [InlineData("graph", "app.dll", "--bogus")]
    public void Bad_arguments_are_explained(params string[] args)
        => Assert.NotNull(ToolArguments.Parse(args).Error);

    [Fact]
    public void The_graph_of_an_assemblys_modules_is_printed()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = GraphCommand.Run(ToolArguments.Parse(new[] { "graph", ThisAssembly, "--format", "text", "--validate" }), output, error);

        Assert.Equal(0, code);
        Assert.Contains("Clock", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void A_missing_assembly_is_reported_with_exit_code_2()
    {
        var error = new StringWriter();

        var code = GraphCommand.Run(ToolArguments.Parse(new[] { "graph", "does-not-exist.dll" }), new StringWriter(), error);

        Assert.Equal(2, code);
        Assert.Contains("not found", error.ToString());
    }

    [Fact]
    public void Validate_prints_nothing_for_a_healthy_assembly_and_exits_with_zero()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = GraphCommand.Run(ToolArguments.Parse(new[] { "validate", ThisAssembly }), output, error);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void An_assembly_built_with_another_version_of_the_library_gets_a_warning()
    {
        var note = ModuleInspector.VersionNote("MyApp", new Version(1, 0, 0, 0), new Version(1, 2, 0, 0));

        Assert.NotNull(note);
        Assert.Contains("MyApp", note);
        Assert.Contains("built with Maxen.DI 1.0.0", note);
        Assert.Contains("this tool uses 1.2.0", note);
        Assert.Contains("--version 1.0.0", note);
    }

    [Theory]
    [InlineData("1.2.0.0", "1.2.0.0")]
    [InlineData("1.2.0.0", "1.2.0.7")]
    [InlineData(null, "1.2.0.0")]
    public void Matching_versions_and_assemblies_without_the_library_get_no_warning(string? built, string tool)
        => Assert.Null(ModuleInspector.VersionNote("MyApp", built is null ? null : Version.Parse(built), Version.Parse(tool)));

    [Fact]
    public void Diffing_an_assembly_with_itself_finds_nothing()
    {
        var output = new StringWriter();

        var code = DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, ThisAssembly }), output, new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains("No differences", output.ToString());
    }

    [Fact]
    public void Diff_and_explain_need_their_extra_arguments()
    {
        Assert.NotNull(ToolArguments.Parse(new[] { "diff", "a.dll" }).Error);
        Assert.NotNull(ToolArguments.Parse(new[] { "explain", "a.dll" }).Error);
        Assert.Null(ToolArguments.Parse(new[] { "explain", "a.dll", "IClock" }).Error);
    }

    [Fact]
    public void Explain_lists_the_registrations_of_a_service()
    {
        var output = new StringWriter();

        var code = ExplainCommand.Run(ToolArguments.Parse(new[] { "explain", ThisAssembly, "Clock" }), output, new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains("Clock", output.ToString());
        Assert.Contains("needed by:", output.ToString());
    }

    [Fact]
    public void Explaining_an_unknown_service_exits_with_one()
    {
        var error = new StringWriter();

        var code = ExplainCommand.Run(ToolArguments.Parse(new[] { "explain", ThisAssembly, "NoSuchThing" }), new StringWriter(), error);

        Assert.Equal(1, code);
        Assert.Contains("No registration found", error.ToString());
    }

    [Fact]
    public void Graph_can_be_written_as_json()
    {
        var output = new StringWriter();

        var code = GraphCommand.Run(ToolArguments.Parse(new[] { "graph", ThisAssembly, "--format", "json" }), output, new StringWriter());

        Assert.Equal(0, code);
        using var document = System.Text.Json.JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.GetProperty("nodes").GetArrayLength() > 0);
    }

    [Fact]
    public void A_snapshot_can_be_saved_and_compared_without_the_old_assembly()
    {
        var file = Path.Combine(Path.GetTempPath(), "maxen-snapshot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var saved = SnapshotCommand.Run(ToolArguments.Parse(new[] { "snapshot", ThisAssembly, file }), new StringWriter(), new StringWriter());
            Assert.Equal(0, saved);

            var same = new StringWriter();
            Assert.Equal(0, DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, "--baseline", file }), same, new StringWriter()));
            Assert.Contains("No differences", same.ToString());

            // Pretend the build used to have an extra registration: the diff must report it as removed.
            var ghost = "{ \"nodes\": [\"Ghost -> Ghost (Singleton)\"], \"edges\": [] }";
            File.WriteAllText(file, ghost);
            var changed = new StringWriter();
            Assert.Equal(1, DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, "--baseline", file }), changed, new StringWriter()));
            Assert.Contains("- registration: Ghost", changed.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_missing_or_broken_baseline_is_reported()
    {
        var error = new StringWriter();
        Assert.Equal(2, DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, "--baseline", "nope.json" }), new StringWriter(), error));
        Assert.Contains("Snapshot not found", error.ToString());

        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "not json");
            var broken = new StringWriter();
            Assert.Equal(2, DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, "--baseline", file }), new StringWriter(), broken));
            Assert.Contains("not a snapshot", broken.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Init_writes_a_module_and_never_overwrites_one()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maxen-init-" + Guid.NewGuid().ToString("N"));
        try
        {
            var args = ToolArguments.Parse(new[] { "init", "Billing", "--namespace", "Shop.Billing", "--directory", directory });
            Assert.Null(args.Error);

            Assert.Equal(0, InitCommand.Run(args, new StringWriter(), new StringWriter()));
            var written = File.ReadAllText(Path.Combine(directory, "BillingModule.cs"));
            Assert.Contains("namespace Shop.Billing;", written);
            Assert.Contains("public sealed class BillingModule : IContainerModule", written);

            var error = new StringWriter();
            Assert.Equal(1, InitCommand.Run(args, new StringWriter(), error));
            Assert.Contains("already exists", error.ToString());

            Assert.Equal(2, InitCommand.Run(ToolArguments.Parse(new[] { "init", "not valid" }), new StringWriter(), new StringWriter()));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Missing_arguments_print_the_usage_text_once()
    {
        var error = new StringWriter();

        Assert.Equal(2, ToolRunner.Run(new[] { "diff" }, new StringWriter(), error));

        var text = error.ToString();
        Assert.Contains("'diff' is missing arguments.", text);
        Assert.Equal(1, text.Split("Usage:").Length - 1);
    }

    [Fact]
    public void A_snapshot_with_the_wrong_shape_is_reported_not_thrown()
    {
        var file = Path.GetTempFileName();
        try
        {
            // Valid JSON, but "nodes" should be a list of strings.
            File.WriteAllText(file, """{ "nodes": 5, "edges": [] }""");
            var error = new StringWriter();

            var code = DiffCommand.Run(ToolArguments.Parse(new[] { "diff", ThisAssembly, "--baseline", file }), new StringWriter(), error);

            Assert.Equal(2, code);
            Assert.Contains("not a snapshot", error.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_module_that_throws_is_reported_with_its_message_and_exit_code_2()
    {
        var directory = NewDirectory();
        try
        {
            var dll = Compile(directory, "Throwing", """
                public sealed class BrokenModule : Maxen.DI.Extensibility.IContainerModule
                {
                    public void Configure(Maxen.DI.ContainerBuilder builder)
                        => throw new System.InvalidOperationException("the database name is not set");
                }
                """);
            var error = new StringWriter();

            var code = GraphCommand.Run(ToolArguments.Parse(new[] { "validate", dll }), new StringWriter(), error);

            Assert.Equal(2, code);
            Assert.Contains("Could not inspect", error.ToString());
            Assert.Contains("the database name is not set", error.ToString());
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Fact]
    public void A_missing_dependency_of_the_inspected_assembly_is_reported_with_exit_code_2()
    {
        var directory = NewDirectory();
        try
        {
            var helper = Compile(directory, "Helper", "public class Helper { }");
            var dll = Compile(directory, "UsesHelper", """
                public sealed class HelperModule : Maxen.DI.Extensibility.IContainerModule
                {
                    public void Configure(Maxen.DI.ContainerBuilder builder)
                        => Maxen.DI.ContainerBuilderExtensions.RegisterInstance(builder, new Helper());
                }
                public sealed class DerivedHelper : Helper { }
                """, helper);

            // The assembly the module needs is gone, as when the tool is pointed at the wrong folder.
            File.Delete(helper);
            var error = new StringWriter();

            var code = GraphCommand.Run(ToolArguments.Parse(new[] { "validate", dll }), new StringWriter(), error);

            Assert.Equal(2, code);
            Assert.Contains("Could not inspect", error.ToString());
            Assert.Contains("Helper", error.ToString());
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    // The tool unloads what it inspected, but Windows keeps the files locked until the unload has really happened.
    // Leftovers in the temp folder are harmless; a failed cleanup must not hide the test's real result.
    private static void DeleteQuietly(string directory)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { Directory.Delete(directory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maxen-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    // Builds a small assembly on disk, so a test can point the tool at modules that misbehave without
    // putting them in this test assembly, which the other tool tests inspect.
    private static string Compile(string directory, string name, string source, params string[] references)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var metadata = platform
            .Append(typeof(Container).Assembly.Location)
            .Concat(references)
            .Select(path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path));

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            name,
            new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source) },
            metadata,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        var path = Path.Combine(directory, name + ".dll");
        Microsoft.CodeAnalysis.Emit.EmitResult result;
        using (var stream = File.Create(path)) result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return path;
    }
}
