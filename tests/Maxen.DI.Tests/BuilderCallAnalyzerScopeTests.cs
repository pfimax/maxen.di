using System.Collections.Immutable;
using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests;

/// <summary>
/// What the registration analyzer may and may not conclude from the code it can see: which calls put services in out
/// of its sight, and which registrations belong to which container when a project builds more than one.
/// </summary>
public class BuilderCallAnalyzerScopeTests
{
    private const string Types = """
        using Maxen.DI;
        using Maxen.DI.Extensibility;
        public interface IRepo { }
        public class Repo : IRepo { }
        public class Service { public Service(IRepo repo) { } }
        """;

    private static async Task<ImmutableArray<Diagnostic>> Run(string source, params MetadataReference[] extra)
    {
        // The adapter and Microsoft's packages are only loaded once a test has used them, so name them outright.
        var adapter = new[] { typeof(MaxenServiceProvider), typeof(ServiceCollection), typeof(IServiceCollection) }
            .Select(t => (MetadataReference)MetadataReference.CreateFromFile(t.Assembly.Location));

        var references = CompilationReferences.All().Concat(adapter).Concat(extra)
            .GroupBy(r => r.Display)
            .Select(g => g.First());

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BuilderCallAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // A second project, compiled on its own and handed over as a finished assembly, the way a project reference is.
    private static MetadataReference Library(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Library",
            new[] { CSharpSyntaxTree.ParseText(source) },
            CompilationReferences.All(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static string InMethod(string body, string more = "")
        => $$"""
            {{Types}}
            {{more}}
            public static class Setup
            {
                public static void Run()
                {
                    {{body}}
                }
            }
            """;

    [Theory]
    [InlineData("builder.RegisterAssemblyTypes(typeof(Setup).Assembly, t => true);")]
    [InlineData("builder.Populate(new Microsoft.Extensions.DependencyInjection.ServiceCollection());")]
    [InlineData("builder.RegisterFactory(typeof(IRepo), _ => new Repo());")]
    [InlineData("builder.RegisterInstance(typeof(IRepo), new Repo());")]
    [InlineData("builder.Add(ServiceDescriptor.ForType(typeof(IRepo), typeof(Repo)));")]
    [InlineData("builder.Register<Repo>().AlsoAs(typeof(IRepo));")]
    [InlineData("((IContainerModule)new RepoModule()).Configure(builder);")]
    public async Task Calls_that_register_services_the_analyzer_cannot_name_switch_the_check_off(string call)
    {
        var diagnostics = await Run(InMethod(
            $$"""
            var builder = new ContainerBuilder();
            {{call}}
            builder.Register<Service>();
            """,
            """
            public class RepoModule : IContainerModule
            {
                void IContainerModule.Configure(ContainerBuilder builder) => builder.Register<IRepo, Repo>();
            }
            """)
            .Replace("using Maxen.DI;", "using Maxen.DI; using Maxen.DI.Extensions.DependencyInjection;"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_service_added_with_TryRegister_counts_as_registered()
    {
        var diagnostics = await Run(InMethod("""
            var builder = new ContainerBuilder();
            builder.TryRegister<IRepo, Repo>();
            builder.Register<Service>();
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_helper_from_another_project_switches_the_check_off()
    {
        // The helper's body is in another assembly, so what it registers can't be read.
        var library = Library("""
            using Maxen.DI;
            namespace Data
            {
                public interface IStore { }
                public class Store : IStore { }
                public static class DataSetup
                {
                    public static ContainerBuilder AddData(this ContainerBuilder builder)
                    {
                        builder.Register<IStore, Store>();
                        return builder;
                    }
                }
            }
            """);

        var diagnostics = await Run("""
            using Maxen.DI;
            using Data;
            public class Report { public Report(IStore store) { } }
            public static class Setup
            {
                public static void Run()
                {
                    var builder = new ContainerBuilder();
                    builder.AddData();
                    builder.Register<Report>();
                }
            }
            """, library);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_helper_in_the_same_project_is_read_and_its_registrations_count()
    {
        var diagnostics = await Run(InMethod(
            """
            var builder = new ContainerBuilder();
            Helpers.AddData(builder);
            builder.Register<Service>();
            """,
            """
            public static class Helpers
            {
                public static void AddData(ContainerBuilder builder) => builder.Register<IRepo, Repo>();
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_registration_on_one_container_does_not_cover_for_another()
    {
        // Only the second container is broken: it never registers IRepo. The first one's registration is not its own.
        var diagnostics = await Run(InMethod("""
            var first = new ContainerBuilder();
            first.Register<IRepo, Repo>();
            first.Register<Service>();

            var second = new ContainerBuilder();
            second.Register<Service>();
            """));

        var missing = Assert.Single(diagnostics, d => d.Id == "MAXEN006");
        Assert.Equal("second.Register<Service>()", Flagged(missing));
    }

    // The code a diagnostic points at, so a test can say which statement was flagged.
    private static string Flagged(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    [Fact]
    public async Task A_call_the_analyzer_cannot_follow_only_blinds_it_to_that_container()
    {
        var diagnostics = await Run(InMethod("""
            var scanned = new ContainerBuilder();
            scanned.RegisterModules(typeof(Setup).Assembly);
            scanned.Register<Service>();

            var plain = new ContainerBuilder();
            plain.Register<Service>();
            """));

        // The scanned container may get IRepo from a module; the plain one can't.
        var missing = Assert.Single(diagnostics, d => d.Id == "MAXEN006");
        Assert.Equal("plain.Register<Service>()", Flagged(missing));
    }

    [Fact]
    public async Task A_child_container_may_rely_on_what_its_parent_registers()
    {
        var diagnostics = await Run(InMethod("""
            var builder = new ContainerBuilder();
            builder.Register<IRepo, Repo>();
            using var parent = builder.Build();
            using var child = parent.CreateChild(c => c.Register<Service>());
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_builder_that_is_handed_around_is_not_judged_on_its_own()
    {
        // The builder came back from a method, so it may already hold registrations.
        var diagnostics = await Run(InMethod(
            """
            var builder = Helpers.Start();
            builder.Register<Service>();
            """,
            """
            public static class Helpers
            {
                public static ContainerBuilder Start()
                {
                    var builder = new ContainerBuilder();
                    builder.Register<IRepo, Repo>();
                    return builder;
                }
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task Lifetimes_are_compared_within_one_container()
    {
        // In the first container both are singletons. A scoped Repo in a second container must not make the first
        // look like a singleton holding a scoped service.
        var diagnostics = await Run(InMethod(
            """
            var first = new ContainerBuilder();
            first.Register<Repo>().AsSingleton();
            first.Register<Cache>().AsSingleton();

            var second = new ContainerBuilder();
            second.Register<Repo>().AsScoped();
            """,
            "public class Cache { public Cache(Repo repo) { } }"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN005");
    }
}
