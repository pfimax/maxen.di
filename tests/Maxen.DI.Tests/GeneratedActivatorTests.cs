using Maxen.DI.Generators;
using Maxen.DI.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Maxen.DI.Tests;

public class GeneratedActivatorTests
{
    private static (string Generated, IReadOnlyList<Diagnostic> Problems) Generate(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = driver.GetRunResult().GeneratedTrees
            .Where(t => t.FilePath.EndsWith("MaxenGeneratedActivators.g.cs", StringComparison.Ordinal))
            .Select(t => t.GetText().ToString())
            .SingleOrDefault() ?? string.Empty;

        // The generated code has to compile as part of the user's project.
        var problems = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return (generated, problems);
    }

    [Fact]
    public void Registering_a_type_in_code_produces_a_constructor_call_for_it()
    {
        var (generated, problems) = Generate("""
            using Maxen.DI;
            public interface IFoo { }
            public class Dep { }
            public class Foo : IFoo { public Foo(Dep dep, string name) { } public Foo() { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder) => builder.Register<IFoo, Foo>();
            }
            """);

        Assert.Empty(problems);
        Assert.Contains("GeneratedActivators.Add(typeof(global::Foo)", generated);
        Assert.Contains("new global::Foo((global::Dep)args[0]!, (string)args[1]!)", generated);

        // Every public constructor is covered, because the container may pick any of them.
        Assert.Contains("new global::Foo()", generated);
    }

    [Fact]
    public void Types_that_cannot_be_called_from_generated_code_are_skipped()
    {
        var (generated, problems) = Generate("""
            using Maxen.DI;
            public abstract class Base { }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Base>();
                    builder.Register(typeof(object), typeof(object));   // not a generic Register, nothing to learn from it
                }
            }
            """);

        Assert.Empty(problems);
        Assert.Equal(string.Empty, generated);
    }

    [Fact]
    public void A_type_registered_twice_is_emitted_once()
    {
        var (generated, _) = Generate("""
            using Maxen.DI;
            public class Foo { }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Foo>();
                    builder.Register<Foo>().AsSingleton();
                }
            }
            """);

        Assert.Single(generated.Split('\n'), line => line.Contains("GeneratedActivators.Add(typeof(global::Foo)"));
    }

    [Fact]
    public void The_container_uses_a_generated_call_instead_of_compiling_one()
    {
        // Pretend the generator saw Stamped; the activator marks what it builds so we can tell who built it.
        GeneratedActivators.Add(
            typeof(Stamped),
            new[] { typeof(IClock) },
            static args => new Stamped((IClock)args[0]!) { ViaGenerator = true });

        var before = GeneratedActivators.PreferForAllConstruction;
        GeneratedActivators.PreferForAllConstruction = true;
        try
        {
            using var container = TestContainer.Build(b =>
            {
                b.Register<IClock, Clock>();
                // Not the generic Register on purpose: that would make the generator write its own entry for Stamped first.
                b.Register(typeof(Stamped), typeof(Stamped));
            });

            Assert.True(container.Resolve<Stamped>().ViaGenerator);
        }
        finally
        {
            GeneratedActivators.PreferForAllConstruction = before;
        }
    }

    [Fact]
    public void Without_the_preference_the_fast_path_still_builds_correctly_for_registered_types()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<NeedsClock>();
        });

        Assert.NotNull(container.Resolve<NeedsClock>().Clock);
    }
}
