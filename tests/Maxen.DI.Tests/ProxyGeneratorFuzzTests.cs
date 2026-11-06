using System.Text;
using Maxen.DI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Maxen.DI.Tests;

/// <summary>
/// Throws randomly shaped interfaces at the proxy generator. Whatever the shape, the generated code must compile,
/// and interfaces made only of things the generator supports must actually get a proxy.
/// </summary>
public class ProxyGeneratorFuzzTests
{
    private static readonly string[] Types =
    {
        "int", "string", "bool", "object", "System.Guid", "int?", "string?", "int[]", "(int, string)",
        "System.Collections.Generic.List<string>", "System.Collections.Generic.IReadOnlyDictionary<string, int>",
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task<int>", "System.Threading.Tasks.ValueTask<string>",
    };

    // Names that are legal identifiers only with an @, to check the emitter escapes them.
    private static readonly string[] Names = { "value", "@object", "@event", "@class", "count", "@string", "item", "@namespace" };

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 60).Select(seed => new object[] { seed });

    // Builds an interface, a class implementing it, and a registration that intercepts it.
    private static (string Source, bool AllSupported) Build(int seed)
    {
        var random = new Random(seed);
        var allSupported = true;
        var contract = new StringBuilder();
        var body = new StringBuilder();

        var members = random.Next(1, 8);
        for (var i = 0; i < members; i++)
        {
            var returns = random.Next(4) == 0 ? "void" : Types[random.Next(Types.Length)];
            var kind = random.Next(20);

            if (kind == 0)
            {
                // Not supported: the generator must leave the whole interface to the runtime proxy.
                allSupported = false;
                contract.AppendLine($"    T Generic{i}<T>(T value);");
                body.AppendLine($"    public T Generic{i}<T>(T value) => value;");
            }
            else if (kind == 1)
            {
                allSupported = false;
                contract.AppendLine($"    event System.EventHandler Event{i};");
                body.AppendLine($"    public event System.EventHandler? Event{i};");
            }
            else if (kind == 2)
            {
                allSupported = false;
                contract.AppendLine($"    void Fill{i}(ref int value);");
                body.AppendLine($"    public void Fill{i}(ref int value) {{ }}");
            }
            else if (kind < 8)
            {
                var type = returns == "void" ? "int" : returns;
                var setter = random.Next(2) == 0;
                contract.AppendLine($"    {type} Prop{i} {{ get;{(setter ? " set;" : "")} }}");
                body.AppendLine($"    public {type} Prop{i} {{ get;{(setter ? " set;" : "")} }} = default!;");
            }
            else
            {
                var parameters = new List<string>();
                var count = random.Next(0, 4);
                for (var p = 0; p < count; p++)
                    parameters.Add($"{Types[random.Next(Types.Length)]} {Names[p]}");

                var list = string.Join(", ", parameters);
                contract.AppendLine($"    {returns} Method{i}({list});");
                body.AppendLine(returns == "void"
                    ? $"    public void Method{i}({list}) {{ }}"
                    : $"    public {returns} Method{i}({list}) => default!;");
            }
        }

        var source = $$"""
            #nullable enable
            using Maxen.DI;
            using Maxen.DI.Interception;
            public interface IFuzz
            {
            {{contract}}}
            public class Fuzz : IFuzz
            {
            {{body}}}
            public class Passthrough : IInterceptor { public void Intercept(IInvocation invocation) => invocation.Proceed(); }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                    => builder.Register<IFuzz, Fuzz>().InterceptWith(new Passthrough());
            }
            """;
        return (source, allSupported);
    }

    private static (string Generated, IReadOnlyList<Diagnostic> Errors) Generate(string source)
    {
        var references = CompilationReferences.All();

        var compilation = CSharpCompilation.Create(
            "FuzzAssembly",
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

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generated_proxies_compile_for_random_interfaces(int seed)
    {
        var (source, allSupported) = Build(seed);

        var (generated, errors) = Generate(source);

        Assert.True(errors.Count == 0,
            $"seed {seed} did not compile: {string.Join(" | ", errors.Select(e => e.ToString()))}\n{source}\n{generated}");

        // Interfaces made only of supported members must be proxied; the others may fall back to the runtime proxy.
        if (allSupported)
            Assert.True(generated.Contains("GeneratedProxyBase, global::IFuzz"), $"seed {seed} got no proxy.\n{source}");
        else
            Assert.Equal(string.Empty, generated);
    }
}
