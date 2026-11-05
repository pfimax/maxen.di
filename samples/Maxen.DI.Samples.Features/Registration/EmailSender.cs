namespace Maxen.DI.Samples.Features.Registration;

/// <summary>Disposable on purpose, to show who owns what.</summary>
internal sealed class EmailSender : IMessageSender, IDisposable
{
    public string Send(string text) => "email: " + text;

    public void Dispose() => Console.WriteLine("  (EmailSender disposed by the container)");
}
