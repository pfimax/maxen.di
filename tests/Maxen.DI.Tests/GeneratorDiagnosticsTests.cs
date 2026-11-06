using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Maxen.DI.Tests;

/// <summary>Runs the generator over small snippets of code and checks which warnings it reports.</summary>
public class GeneratorDiagnosticsTests
{
    private static IReadOnlyList<Diagnostic> Run(string source)
    {
        // Everything loaded in this process is good enough as the snippet's references (runtime + Maxen.DI).
        var references = CompilationReferences.All();

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return diagnostics;
    }

    [Fact]
    public void A_singleton_that_takes_a_scoped_service_is_flagged()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            [Service(ServiceLifetime.Scoped)] public class Repo { }
            [Service(ServiceLifetime.Singleton)] public class Cache { public Cache(Repo repo) { } }
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN004");
        Assert.Contains("Cache", captive.GetMessage());
        Assert.Contains("Repo", captive.GetMessage());
    }

    [Fact]
    public void A_scoped_service_may_take_a_singleton()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            [Service(ServiceLifetime.Singleton)] public class Cache { }
            [Service(ServiceLifetime.Scoped)] public class Repo { public Repo(Cache cache) { } }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN004");
    }

    [Fact]
    public void The_captive_check_follows_the_service_type_not_just_the_class()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            public interface IRepo { }
            [Service(ServiceLifetime.Scoped, ServiceType = typeof(IRepo))] public class Repo : IRepo { }
            [Service(ServiceLifetime.Singleton)] public class Cache { public Cache(IRepo repo) { } }
            """);

        Assert.Contains(diagnostics, d => d.Id == "MAXEN004");
    }

    [Fact]
    public void A_transient_that_takes_a_scoped_service_is_fine()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            [Service(ServiceLifetime.Scoped)] public class Repo { }
            [Service(ServiceLifetime.Transient)] public class Handler { public Handler(Repo repo) { } }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN004");
    }

    [Fact]
    public void Intercepting_a_class_that_is_not_registered_as_an_interface_is_flagged()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            using Maxen.DI.Interception;
            public class Passthrough : IInterceptor { public void Intercept(IInvocation invocation) => invocation.Proceed(); }
            [Service, Intercept(typeof(Passthrough))] public class Plain { }
            """);

        var warning = Assert.Single(diagnostics, d => d.Id == "MAXEN002");
        Assert.Contains("interface", warning.GetMessage());
    }

    [Fact]
    public void An_interceptor_without_a_parameterless_constructor_is_flagged()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            using Maxen.DI.Interception;
            public interface IThing { }
            public class Needy : IInterceptor
            {
                public Needy(int value) { }
                public void Intercept(IInvocation invocation) => invocation.Proceed();
            }
            [Service(ServiceType = typeof(IThing)), Intercept(typeof(Needy))] public class Thing : IThing { }
            """);

        var warning = Assert.Single(diagnostics, d => d.Id == "MAXEN002");
        Assert.Contains("parameterless", warning.GetMessage());
    }

    [Fact]
    public void A_decorator_that_cannot_take_the_service_it_wraps_is_flagged()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            public interface IThing { }
            [Decorator(typeof(IThing))] public class Lonely : IThing { }
            """);

        var warning = Assert.Single(diagnostics, d => d.Id == "MAXEN008");
        Assert.Contains("Lonely", warning.GetMessage());
        Assert.Contains("IThing", warning.GetMessage());
    }

    [Fact]
    public void A_decorator_that_takes_its_inner_service_is_fine()
    {
        var diagnostics = Run("""
            using Maxen.DI;
            public interface IThing { }
            [Decorator(typeof(IThing))] public class Wrapper : IThing { public Wrapper(IThing inner) { } }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN008");
    }
}
