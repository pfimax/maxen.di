namespace Maxen.DI.Samples.WebApi;

public sealed class QuietGreeter : IGreeter
{
    public string Greet(string name) => $"psst, {name}";
}
