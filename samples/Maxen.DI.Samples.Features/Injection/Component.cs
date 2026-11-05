namespace Maxen.DI.Samples.Features.Injection;

/// <summary>Gets one service through a property and another through a method, after the constructor ran.</summary>
internal sealed class Component
{
    [Inject]
    public Settings? Settings { get; set; }

    public string? Source { get; private set; }

    [Inject]
    public void Initialize(Settings settings) => Source = "method got " + settings.Environment;
}
