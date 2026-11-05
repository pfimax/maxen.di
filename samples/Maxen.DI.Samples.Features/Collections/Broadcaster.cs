using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.Collections;

internal sealed class Broadcaster
{
    private readonly IEnumerable<IMessageSender> _senders;

    public Broadcaster(IEnumerable<IMessageSender> senders) => _senders = senders;

    public IEnumerable<string> SendToAll(string text) => _senders.Select(s => s.Send(text));
}
