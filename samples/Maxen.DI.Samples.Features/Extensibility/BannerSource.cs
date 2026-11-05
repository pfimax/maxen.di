using Maxen.DI.Extensibility;

namespace Maxen.DI.Samples.Features.Extensibility;

/// <summary>Answers requests for IBanner on the fly, with no registration at all.</summary>
internal sealed class BannerSource : IRegistrationSource
{
    private sealed class Banner : IBanner
    {
        public string Text => "made up by a registration source";
    }

    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        if (key.Type == typeof(IBanner))
            yield return ServiceDescriptor.ForFactory(typeof(IBanner), _ => new Banner(), key.Key);
    }
}
