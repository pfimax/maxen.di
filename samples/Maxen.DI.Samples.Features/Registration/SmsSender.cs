namespace Maxen.DI.Samples.Features.Registration;

internal sealed class SmsSender : IMessageSender
{
    public string Send(string text) => "sms: " + text;
}
