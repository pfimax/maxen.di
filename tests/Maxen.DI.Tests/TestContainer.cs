namespace Maxen.DI.Tests;

/// <summary>Keeps the tests short: configure a builder and get the container back.</summary>
internal static class TestContainer
{
    public static Container Build(Action<ContainerBuilder> configure)
    {
        var builder = new ContainerBuilder();
        configure(builder);
        return builder.Build();
    }
}
