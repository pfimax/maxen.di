using Maxen.DI.Extensibility;

namespace Maxen.DI.Samples.WebApi;

/// <summary>Swaps the greeter for a quiet one while the "quiet" feature is on.</summary>
public sealed class QuietGreetingModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        builder.Replace<IGreeter, QuietGreeter>().AsSingleton();
    }
}
