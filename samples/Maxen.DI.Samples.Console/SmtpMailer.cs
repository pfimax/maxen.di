namespace Maxen.DI.Samples.Console;

/// <summary>Pretends to talk to a mail server. Used when no profile is active.</summary>
public sealed class SmtpMailer : IMailer
{
    public void Send(string to, string message)
        => System.Console.WriteLine($"[smtp] sent '{message}' to {to}");
}
