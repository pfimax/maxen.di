namespace Maxen.DI.Tests.Fixtures;

/// <summary>A tiny interface for the [Intercept] attribute tests; kept separate so its interceptors affect nothing else.</summary>
public interface ITallyService
{
    int Add(int a, int b);
}
