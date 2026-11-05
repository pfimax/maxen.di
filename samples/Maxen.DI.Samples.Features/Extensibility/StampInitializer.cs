using Maxen.DI.Extensibility;
using Maxen.DI.Pipeline;

namespace Maxen.DI.Samples.Features.Extensibility;

/// <summary>Runs on every freshly built object, which is how property injection is implemented too.</summary>
internal sealed class StampInitializer : IInstanceInitializer
{
    public void Initialize(object instance, ResolveContext context)
    {
        if (instance is DataClient client) client.Stamped = new DateTime(2031, 1, 1);
    }
}
