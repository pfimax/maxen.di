using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.Keyed;

internal sealed class Notifier
{
    private readonly IMessageSender _sender;

    public Notifier([FromKey("sms")] IMessageSender sender) => _sender = sender;

    public string Notify(string text) => _sender.Send(text);
}
