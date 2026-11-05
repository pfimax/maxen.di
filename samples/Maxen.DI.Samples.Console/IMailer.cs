namespace Maxen.DI.Samples.Console;

/// <summary>Something that sends messages. The sample swaps real and fake versions of it.</summary>
public interface IMailer
{
    void Send(string to, string message);
}
