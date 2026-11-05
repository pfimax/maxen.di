using System.Collections.Immutable;
using Maxen.DI.CodeFixes;
using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Maxen.DI.Tests;

/// <summary>Applies the "make it scoped" quick fix to small snippets and checks the rewritten source.</summary>
public class CaptiveLifetimeCodeFixTests
{
    // Builds a document, finds the diagnostic with the given id (from the analyzer or the generator), and applies the first fix.
    internal static async Task<string> ApplyFix(string source, string diagnosticId, int actionIndex = 0, CodeFixProvider? provider = null)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Snippet", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .AddMetadataReferences(references);
        var document = project.AddDocument("Snippet.cs", source);

        var compilation = (await document.Project.GetCompilationAsync())!;

        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BuilderCallAnalyzer()))
            .GetAnalyzerDiagnosticsAsync());
        CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var generated);
        diagnostics.AddRange(generated);

        var diagnostic = diagnostics.Single(d => d.Id == diagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await (provider ?? new CaptiveLifetimeCodeFix()).RegisterCodeFixesAsync(context);

        var operations = await actions[actionIndex].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return (await changed.GetDocument(document.Id)!.GetTextAsync()).ToString();
    }

    private const string RegistrationSnippet = """
        using Maxen.DI;
        public class Repo { }
        public class Cache { public Cache(Repo repo) { } }
        public static class Setup
        {
            public static void Configure(ContainerBuilder builder)
            {
                builder.Register<Repo>().AsScoped();
                builder.Register<Cache>().AsSingleton();
            }
        }
        """;

    [Fact]
    public async Task A_singleton_registration_can_be_made_scoped()
    {
        var fixedSource = await ApplyFix(RegistrationSnippet, "MAXEN005");

        Assert.Contains("builder.Register<Cache>().AsScoped();", fixedSource);
        Assert.DoesNotContain("AsSingleton", fixedSource);
    }

    [Fact]
    public async Task A_singleton_registration_can_be_made_transient()
    {
        var fixedSource = await ApplyFix(RegistrationSnippet, "MAXEN005", actionIndex: 1);

        Assert.Contains("builder.Register<Cache>().AsTransient();", fixedSource);
    }

    [Fact]
    public async Task A_singleton_service_attribute_can_be_made_scoped()
    {
        var fixedSource = await ApplyFix("""
            using Maxen.DI;
            [Service(ServiceLifetime.Scoped)] public class Repo { }
            [Service(ServiceLifetime.Singleton)] public class Cache { public Cache(Repo repo) { } }
            """, "MAXEN004");

        Assert.Contains("[Service(ServiceLifetime.Scoped)] public class Cache", fixedSource);
    }
}
