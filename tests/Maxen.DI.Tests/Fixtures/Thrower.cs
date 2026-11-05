namespace Maxen.DI.Tests.Fixtures;

public sealed class Thrower : IThrower
{
    public void Boom() => throw new InvalidOperationException("boom");
}
