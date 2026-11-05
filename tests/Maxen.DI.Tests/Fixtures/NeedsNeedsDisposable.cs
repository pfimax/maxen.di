namespace Maxen.DI.Tests.Fixtures;

/// <summary>One step further away from <see cref="Disposable"/>, to test captive dependencies through a middleman.</summary>
public class NeedsNeedsDisposable
{
    public NeedsNeedsDisposable(NeedsDisposable inner) { }
}
