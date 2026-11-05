namespace Maxen.DI.Tests.Fixtures;

/// <summary>Never registered explicitly; <see cref="AutoMadeSource"/> supplies it.</summary>
public sealed class AutoMade
{
    public string Value => "auto";
}
