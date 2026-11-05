namespace Maxen.DI.Tests.Fixtures;

/// <summary>The real service behind <see cref="IEchoer"/>.</summary>
public sealed class Echoer : IEchoer
{
    public T Echo<T>(T value) => value;
}
