namespace Maxen.DI.Tests.Fixtures;

/// <summary>Has a generic method, which the generator can't forward, so it must fall back to the runtime proxy.</summary>
public interface IEchoer
{
    T Echo<T>(T value);
}
