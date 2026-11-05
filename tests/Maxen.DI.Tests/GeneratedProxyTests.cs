using Maxen.DI.Interception;
using Maxen.DI.Generators;
using Maxen.DI.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Maxen.DI.Tests;

public class GeneratedProxyTests
{
    [Fact]
    public async Task A_generated_proxy_forwards_properties_methods_and_async_calls()
    {
        var recorder = new CallRecorder();
        using var container = TestContainer.Build(b =>
            b.Register<IRich, Rich>().AsSingleton().InterceptWith(recorder));

        var rich = container.Resolve<IRich>();

        // Not the runtime DispatchProxy: the generator wrote a class for this interface.
        Assert.StartsWith("GeneratedProxy", rich.GetType().Name);

        Assert.Equal(5, rich.Add(2, 3));
        rich.Name = "changed";
        Assert.Equal("changed", rich.Name);
        Assert.Equal("loaded x", await rich.LoadAsync("x"));
        Assert.Equal(1, rich.Count);
        rich.Reset();
        Assert.Equal(0, rich.Count);

        Assert.Equal(
            new[] { "Add", "set_Name", "get_Name", "LoadAsync", "get_Count", "Reset", "get_Count" },
            recorder.Calls);
    }

    [Fact]
    public void Interceptors_can_change_arguments_and_short_circuit_through_a_generated_proxy()
    {
        using var container = TestContainer.Build(b =>
            b.Register<IRich, Rich>().InterceptWith(new DoubleArgumentsInterceptor()));

        // The interceptor doubles both arguments before the real method sees them.
        Assert.Equal(10, container.Resolve<IRich>().Add(2, 3));
    }

    [Fact]
    public void Exceptions_from_the_service_are_not_wrapped_by_a_generated_proxy()
    {
        using var container = TestContainer.Build(b =>
            b.Register<IThrower, Thrower>().InterceptWith(new CallRecorder()));

        Assert.StartsWith("GeneratedProxy", container.Resolve<IThrower>().GetType().Name);
        Assert.Throws<InvalidOperationException>(() => container.Resolve<IThrower>().Boom());
    }

    [Fact]
    public void An_interface_with_a_generic_method_falls_back_to_the_runtime_proxy()
    {
        using var container = TestContainer.Build(b =>
            b.Register<IEchoer, Echoer>().InterceptWith(new CallRecorder()));

        var echoer = container.Resolve<IEchoer>();

        Assert.DoesNotContain("GeneratedProxy", echoer.GetType().Name);
        Assert.Equal(7, echoer.Echo(7));
    }

    private static (string Generated, IReadOnlyList<Diagnostic> Errors) Generate(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

        var compilation = CSharpCompilation.Create(
            "Snippet",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = driver.GetRunResult().GeneratedTrees
            .Where(t => t.FilePath.EndsWith("MaxenGeneratedProxies.g.cs", StringComparison.Ordinal))
            .Select(t => t.GetText().ToString())
            .SingleOrDefault() ?? string.Empty;

        return (generated, output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList());
    }

    [Fact]
    public void The_generated_proxy_compiles_for_awkward_interfaces()
    {
        var (generated, errors) = Generate("""
            using Maxen.DI;
            using Maxen.DI.Interception;
            public interface IBase { string Id { get; } }
            public interface IAwkward : IBase
            {
                int Value { get; set; }
                void Run(string @object, int @event);
                System.Collections.Generic.List<string> Names(int[] ids);
            }
            public class Awkward : IAwkward
            {
                public string Id => "";
                public int Value { get; set; }
                public void Run(string @object, int @event) { }
                public System.Collections.Generic.List<string> Names(int[] ids) => new();
            }
            public class Passthrough : IInterceptor { public void Intercept(IInvocation invocation) => invocation.Proceed(); }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                    => builder.Register<IAwkward, Awkward>().InterceptWith(new Passthrough());
            }
            """);

        Assert.Empty(errors);
        Assert.Contains("GeneratedProxyBase, global::IAwkward", generated);
    }

    [Fact]
    public void Interfaces_the_generator_cannot_forward_are_left_out()
    {
        var (generated, errors) = Generate("""
            using Maxen.DI;
            using Maxen.DI.Interception;
            public interface IGeneric { T Echo<T>(T value); }
            public interface IWithEvent { event System.EventHandler Changed; }
            public interface IWithRef { void Fill(ref int value); }
            public class G : IGeneric { public T Echo<T>(T value) => value; }
            public class E : IWithEvent { public event System.EventHandler? Changed; }
            public class R : IWithRef { public void Fill(ref int value) { } }
            public class Passthrough : IInterceptor { public void Intercept(IInvocation invocation) => invocation.Proceed(); }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<IGeneric, G>().InterceptWith(new Passthrough());
                    builder.Register<IWithEvent, E>().InterceptWith(new Passthrough());
                    builder.Register<IWithRef, R>().InterceptWith(new Passthrough());
                }
            }
            """);

        Assert.Empty(errors);
        Assert.Equal(string.Empty, generated);
    }
}
