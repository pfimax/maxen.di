namespace Maxen.DI.Samples.WebApi;

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}";
}
