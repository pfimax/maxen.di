using System.Collections.Immutable;
using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Maxen.DI.Tests;

/// <summary>
/// Two mistakes the container rejects at run time that the analyzer can already see in the code: a singleton that
/// reaches a scoped service through a transient, and a scoped service asked for from the root container.
/// </summary>
public class BuilderCallAnalyzerReachTests
{
    private static async Task<ImmutableArray<Diagnostic>> Run(string body, string registrations)
    {
        var source = $$"""
            using Maxen.DI;
            public class Session { }
            public class Query { public Query(Session session) { } }
            public class Report { public Report(Query query) { } }
            public class Cache { public Cache(Report report) { } }
            public class TwoWays { public TwoWays() { } public TwoWays(Session session) { } }
            public class Holder { public Holder(TwoWays twoWays) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder, Container container, IScope scope)
                {
                    {{registrations}}
                    {{body}}
                }
            }
            """;

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            CompilationReferences.All(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BuilderCallAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task A_singleton_reaching_a_scoped_service_through_a_transient_is_flagged()
    {
        var diagnostics = await Run(string.Empty, """
            builder.Register<Session>().AsScoped();
            builder.Register<Query>();
            builder.Register<Report>().AsSingleton();
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN005");
        Assert.Contains("Singleton 'Report' takes the scoped service 'Session' through the transient 'Query'", captive.GetMessage());
    }

    [Fact]
    public async Task Several_transients_in_a_row_are_followed()
    {
        var diagnostics = await Run(string.Empty, """
            builder.Register<Session>().AsScoped();
            builder.Register<Query>();
            builder.Register<Report>();
            builder.Register<Cache>().AsSingleton();
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN005");
        Assert.Contains("through the transients 'Report' -> 'Query'", captive.GetMessage());
    }

    [Fact]
    public async Task A_direct_dependency_keeps_its_plain_message()
    {
        var diagnostics = await Run(string.Empty, """
            builder.Register<Session>().AsScoped();
            builder.Register<Query>().AsSingleton();
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN005");
        Assert.Contains("takes the scoped service 'Session' in its constructor,", captive.GetMessage());
    }

    [Fact]
    public async Task A_singleton_or_scoped_service_in_between_is_judged_on_its_own()
    {
        // Query is the one at fault here, and is flagged; Report only takes a singleton.
        var diagnostics = await Run(string.Empty, """
            builder.Register<Session>().AsScoped();
            builder.Register<Query>().AsSingleton();
            builder.Register<Report>().AsSingleton();
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN005");
        Assert.Contains("'Query'", captive.GetMessage());
    }

    [Fact]
    public async Task A_transient_with_a_choice_of_constructors_is_not_followed()
    {
        // The container picks TwoWays' constructor at run time, so nothing is claimed about what it takes.
        var diagnostics = await Run(string.Empty, """
            builder.Register<Session>().AsScoped();
            builder.Register<TwoWays>();
            builder.Register<Holder>().AsSingleton();
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN005");
    }

    [Theory]
    [InlineData("container.Resolve<Session>();")]
    [InlineData("container.TryResolve<Session>();")]
    public async Task A_scoped_service_resolved_from_the_root_is_flagged(string resolve)
    {
        var diagnostics = await Run(resolve, "builder.Register<Session>().AsScoped();");

        var fromRoot = Assert.Single(diagnostics, d => d.Id == "MAXEN010");
        Assert.Contains("Session", fromRoot.GetMessage());
    }

    [Fact]
    public async Task Resolving_it_from_a_scope_is_fine()
    {
        var diagnostics = await Run("scope.Resolve<Session>();", "builder.Register<Session>().AsScoped();");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN010");
    }

    [Fact]
    public async Task Singletons_and_transients_may_be_resolved_from_the_root()
    {
        var diagnostics = await Run(
            "container.Resolve<Session>(); container.Resolve<TwoWays>();",
            "builder.Register<Session>().AsSingleton(); builder.Register<TwoWays>();");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN010");
    }

    [Theory]
    [InlineData("builder.Options.ValidateScopes = false;")]
    [InlineData("var strict = System.Environment.ProcessorCount > 1; builder.Options.ValidateScopes = strict;")]
    public async Task With_scope_validation_switched_off_the_root_may_hand_out_scoped_services(string option)
    {
        var diagnostics = await Run("container.Resolve<Session>();", $"{option} builder.Register<Session>().AsScoped();");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN010");
    }

    [Fact]
    public async Task Leaving_scope_validation_on_explicitly_keeps_the_warning()
    {
        var diagnostics = await Run(
            "container.Resolve<Session>();",
            "builder.Options.ValidateScopes = true; builder.Register<Session>().AsScoped();");

        Assert.Single(diagnostics, d => d.Id == "MAXEN010");
    }
}
