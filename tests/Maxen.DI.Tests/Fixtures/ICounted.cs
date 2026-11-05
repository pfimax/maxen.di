namespace Maxen.DI.Tests.Fixtures;

/// <summary>A base interface, so the generated proxy has to cover inherited members too.</summary>
public interface ICounted
{
    int Count { get; }
}
