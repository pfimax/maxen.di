namespace Maxen.DI.Tool;

/// <summary>Writes a starter <c>IContainerModule</c>, the unit the other commands inspect.</summary>
internal static class InitCommand
{
    public static int Run(ToolArguments arguments, TextWriter output, TextWriter error)
    {
        var name = arguments.ModuleName.EndsWith("Module", StringComparison.Ordinal) ? arguments.ModuleName : arguments.ModuleName + "Module";

        if (!IsIdentifier(name) || !arguments.Namespace.Split('.').All(IsIdentifier))
        {
            error.WriteLine($"'{arguments.Namespace}.{name}' is not a valid class name.");
            return 2;
        }

        System.IO.Directory.CreateDirectory(arguments.Directory);
        var path = Path.Combine(arguments.Directory, name + ".cs");
        if (File.Exists(path))
        {
            // Never overwrite somebody's work.
            error.WriteLine($"{Path.GetFullPath(path)} already exists.");
            return 1;
        }

        var text = string.Join(Environment.NewLine, new[]
        {
            "using Maxen.DI;",
            "using Maxen.DI.Extensibility;",
            "",
            $"namespace {arguments.Namespace};",
            "",
            "/// <summary>Registers this part of the application. Public with a parameterless constructor, so tools and RegisterModules can find it.</summary>",
            $"public sealed class {name} : IContainerModule",
            "{",
            "    public void Configure(ContainerBuilder builder)",
            "    {",
            "        // builder.Register<IFoo, Foo>().AsSingleton();",
            "    }",
            "}",
            "",
        });
        File.WriteAllText(path, text);

        output.WriteLine($"Wrote {Path.GetFullPath(path)}");
        return 0;
    }

    private static bool IsIdentifier(string text)
        => text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');
}
