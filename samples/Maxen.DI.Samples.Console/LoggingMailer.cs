namespace Maxen.DI.Samples.Console;

/// <summary>A decorator: wraps whichever mailer is registered and logs each call, without that mailer knowing.</summary>
public sealed class LoggingMailer : IMailer
{
    private readonly IMailer _inner;

    public LoggingMailer(IMailer inner) => _inner = inner;

    public void Send(string to, string message)
    {
        System.Console.WriteLine($"[log] sending to {to}");
        _inner.Send(to, message);
    }
}
