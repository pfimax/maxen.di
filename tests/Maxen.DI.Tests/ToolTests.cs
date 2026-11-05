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
}
