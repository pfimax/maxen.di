using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Console;

/// <summary>
/// Registered by the source generator from the attributes alone. [Intercept] puts CountingInterceptor in front of it;
/// the proxy that does so is written at compile time, so this stays native AOT friendly.
/// </summary>
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IGreeting))]
[Intercept(typeof(CountingInterceptor))]
public sealed class Greeting : IGreeting
{
    public string For(string name) => $"Hello, {name}";
}
