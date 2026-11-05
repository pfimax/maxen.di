namespace Maxen.DI.Samples.Console;

/// <summary>A harmless stand-in for development: prints instead of sending.</summary>
public sealed class ConsoleMailer : IMailer
{
    public void Send(string to, string message)
        => System.Console.WriteLine($"[dev] would send '{message}' to {to}");
}
