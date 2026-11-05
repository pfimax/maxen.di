namespace Maxen.DI.Samples.Features.Options;

internal sealed class MailOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;
}
