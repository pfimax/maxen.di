using System.Collections.Immutable;
using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Maxen.DI.Tests;

/// <summary>Runs the registration analyzer over small snippets and checks what it reports.</summary>
public class BuilderCallAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> Run(string source)
    {
        var references = CompilationReferences.All();

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BuilderCallAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task A_singleton_taking_a_scoped_service_is_flagged()
    {
        var diagnostics = await Run("""
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
            """);

        var captive = Assert.Single(diagnostics, d => d.Id == "MAXEN005");
        Assert.Contains("Cache", captive.GetMessage());
        Assert.Contains("Repo", captive.GetMessage());
    }

    [Fact]
    public async Task The_service_type_is_followed_not_just_the_class()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public interface IRepo { }
            public class Repo : IRepo { }
            public class Cache { public Cache(IRepo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<IRepo, Repo>().AsScoped();
                    builder.Register<Cache>().AsSingleton();
                }
            }
            """);

        Assert.Single(diagnostics, d => d.Id == "MAXEN005");
    }

    [Fact]
    public async Task Fluent_calls_in_between_do_not_hide_the_lifetime()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public class Repo { }
            public class Cache { public Cache(Repo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Repo>().ExternallyOwned().AsScoped();
                    builder.Register<Cache>().WithMetadata("a", 1).AsSingleton();
                }
            }
            """);

        Assert.Single(diagnostics, d => d.Id == "MAXEN005");
    }

    [Fact]
    public async Task A_scoped_service_may_take_a_singleton()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public class Cache { }
            public class Repo { public Repo(Cache cache) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Cache>().AsSingleton();
                    builder.Register<Repo>().AsScoped();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN005");
    }

    [Fact]
    public async Task Transient_and_keyed_registrations_are_left_alone()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public class Repo { }
            public class Cache { public Cache(Repo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Repo>().AsScoped().WithKey("a");   // keyed: which one a constructor gets is unknowable here
                    builder.Register<Cache>().AsSingleton();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN005");
    }

    [Fact]
    public async Task The_last_registration_of_a_service_is_the_one_that_counts()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public class Repo { }
            public class Cache { public Cache(Repo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Repo>().AsScoped();
                    builder.Register<Repo>().AsSingleton();   // wins, so Cache is fine
                    builder.Register<Cache>().AsSingleton();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN005");
    }

    private static string Setup(string registrations, string types = "")
        => $$"""
            using Maxen.DI;
            using Maxen.DI.Scanning;
            public interface IRepo { }
            public class Repo : IRepo { }
            public class Service { public Service(IRepo repo) { } }
            {{types}}
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    {{registrations}}
                }
            }
            """;

    [Fact]
    public async Task A_constructor_needing_an_unregistered_service_is_flagged()
    {
        var diagnostics = await Run(Setup("builder.Register<Service>();"));

        var missing = Assert.Single(diagnostics, d => d.Id == "MAXEN006");
        Assert.Contains("IRepo", missing.GetMessage());
    }

    [Fact]
    public async Task A_registered_dependency_satisfies_the_constructor()
    {
        var diagnostics = await Run(Setup("builder.Register<IRepo, Repo>(); builder.Register<Service>();"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task Factory_and_instance_registrations_count_too()
    {
        var diagnostics = await Run(Setup("builder.RegisterFactory<IRepo>(_ => new Repo()); builder.Register<Service>();"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task Anything_that_registers_services_out_of_sight_switches_the_check_off()
    {
        // A scan could register IRepo, so saying it is missing would be a guess.
        var diagnostics = await Run(Setup(
            "builder.Register<Service>(); builder.Scan(typeof(Setup).Assembly, s => s.AsImplementedInterfaces());"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task Parameters_with_defaults_collections_and_framework_types_are_fine()
    {
        var diagnostics = await Run(Setup(
            "builder.Register<Needy>();",
            """
            public class Needy
            {
                public Needy(IRepo? maybe, System.Collections.Generic.IEnumerable<IRepo> all, System.Func<IRepo> make, IScope scope, string name = "x") { }
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task With_several_constructors_nothing_is_claimed()
    {
        var diagnostics = await Run(Setup(
            "builder.Register<Flexible>();",
            "public class Flexible { public Flexible() { } public Flexible(IRepo repo) { } }"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    [Fact]
    public async Task A_service_attribute_counts_as_registered()
    {
        var diagnostics = await Run(Setup(
            "builder.Register<Service>();",
            "[Maxen.DI.Service(Maxen.DI.ServiceLifetime.Singleton, ServiceType = typeof(IRepo))] public class Attributed : IRepo { }"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    private static string DisposableSetup(string registration, string resolveReceiver)
        => $$"""
            using Maxen.DI;
            public class Handle : System.IDisposable { public void Dispose() { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder, Container container, IScope scope)
                {
                    {{registration}}
                    var handle = {{resolveReceiver}}.Resolve<Handle>();
                }
            }
            """;

    [Fact]
    public async Task A_disposable_transient_resolved_from_the_root_is_flagged()
    {
        var diagnostics = await Run(DisposableSetup("builder.Register<Handle>();", "container"));

        var leak = Assert.Single(diagnostics, d => d.Id == "MAXEN007");
        Assert.Contains("Handle", leak.GetMessage());
    }

    [Fact]
    public async Task Resolving_the_same_service_from_a_scope_is_fine()
    {
        var diagnostics = await Run(DisposableSetup("builder.Register<Handle>();", "scope"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN007");
    }

    [Fact]
    public async Task A_singleton_or_scoped_disposable_is_not_flagged_at_the_root()
    {
        var singleton = await Run(DisposableSetup("builder.Register<Handle>().AsSingleton();", "container"));
        var scoped = await Run(DisposableSetup("builder.Register<Handle>().AsScoped();", "container"));

        Assert.DoesNotContain(singleton, d => d.Id == "MAXEN007");
        Assert.DoesNotContain(scoped, d => d.Id == "MAXEN007");
    }

    [Fact]
    public async Task A_transient_that_is_not_disposable_is_not_flagged()
    {
        var diagnostics = await Run("""
            using Maxen.DI;
            public class Plain { }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder, Container container)
                {
                    builder.Register<Plain>();
                    container.Resolve<Plain>();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN007");
    }

    private static string DeferredSetup(string parameter, string lifetime)
        => $$"""
            using Maxen.DI;
            public class Repo { }
            public class Cache { public Cache({{parameter}}) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Repo>().{{lifetime}}();
                    builder.Register<Cache>().AsSingleton();
                }
            }
            """;

    [Theory]
    [InlineData("System.Lazy<Repo> repo")]
    [InlineData("System.Func<Repo> make")]
    [InlineData("System.Func<int, Repo> make")]
    public async Task A_singleton_holding_a_lazy_or_func_of_a_scoped_service_is_flagged(string parameter)
    {
        var diagnostics = await Run(DeferredSetup(parameter, "AsScoped"));

        var warning = Assert.Single(diagnostics, d => d.Id == "MAXEN009");
        Assert.Contains("Cache", warning.GetMessage());
        Assert.Contains("Repo", warning.GetMessage());
    }

    [Fact]
    public async Task A_lazy_of_a_singleton_or_transient_is_fine()
    {
        var singleton = await Run(DeferredSetup("System.Lazy<Repo> repo", "AsSingleton"));
        var transient = await Run(DeferredSetup("System.Func<Repo> make", "AsTransient"));

        Assert.DoesNotContain(singleton, d => d.Id == "MAXEN009");
        Assert.DoesNotContain(transient, d => d.Id == "MAXEN009");
    }

    [Fact]
    public async Task Microsoft_key_attributes_are_not_reported_as_missing_registrations()
    {
        // The attributes are declared in the snippet so the test does not depend on which assemblies happen to be loaded.
        var diagnostics = await Run("""
            using Maxen.DI;
            namespace Microsoft.Extensions.DependencyInjection
            {
                public sealed class ServiceKeyAttribute : System.Attribute { }
                public sealed class FromKeyedServicesAttribute : System.Attribute { public FromKeyedServicesAttribute(object key) { } }
            }
            public interface IRepo { }
            public class Tenant
            {
                public Tenant(
                    [Microsoft.Extensions.DependencyInjection.ServiceKey] Settings key,
                    [Microsoft.Extensions.DependencyInjection.FromKeyedServices("main")] IRepo repo) { }
            }
            public class Settings { }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder) => builder.Register<Tenant>().WithKey("t1");
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MAXEN006");
    }

    // The generated RegisterGeneratedServices lives in a namespace named after the assembly. Both an ordinary name and
    // one starting with "Maxen.DI" (like this repository's samples) must keep the check on.
    [Theory]
    [InlineData("MyApp")]
    [InlineData("Maxen.DI.Samples.Shop")]
    public async Task Generated_registrations_keep_the_missing_registration_check_on(string assemblyNamespace)
    {
        var diagnostics = await Run($$"""
            using Maxen.DI;
            namespace {{assemblyNamespace}}
            {
                // Stands in for the method the source generator writes.
                internal static class GeneratedRegistrations
                {
                    internal static ContainerBuilder RegisterGeneratedServices(this ContainerBuilder builder) => builder;
                }

                public interface IClock { }
                [Service(ServiceType = typeof(IClock))] public class Clock : IClock { }

                public interface IRepo { }
                public class Report { public Report(IClock clock) { } }
                public class Orders { public Orders(IRepo repo) { } }

                public static class Setup
                {
                    public static void Configure(ContainerBuilder builder)
                    {
                        builder.RegisterGeneratedServices();
                        builder.Register<Report>();
                        builder.Register<Orders>();
                    }
                }
            }
            """);

        // IClock comes from a [Service] class, so Report is fine; nothing registers IRepo, so Orders is flagged.
        var missing = Assert.Single(diagnostics, d => d.Id == "MAXEN006");
        Assert.Contains("IRepo", missing.GetMessage());
    }
}
