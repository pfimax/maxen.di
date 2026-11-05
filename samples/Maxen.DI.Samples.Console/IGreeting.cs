namespace Maxen.DI.Samples.Console;

/// <summary>A service registered purely with attributes: see Greeting, ShoutingGreeting and CountingInterceptor.</summary>
public interface IGreeting
{
    string For(string name);
}
