namespace Maxen.DI.Samples.Features.Decorators;

internal sealed class Greeter : IGreeter
{
    public string Greet(string name) => "Hello, " + name;
}
