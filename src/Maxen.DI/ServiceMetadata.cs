namespace Maxen.DI;

/// <summary>Names of the metadata entries the framework itself understands.</summary>
public static class ServiceMetadata
{
    /// <summary>Marks a registration whose constructor takes some values at resolve time.</summary>
    public const string RuntimeArguments = "maxen.runtime-arguments";

    /// <summary>The consumer type a <c>WhenInjectedInto</c> registration is limited to. Lets validation spot ones that can never match.</summary>
    public const string InjectedInto = "maxen.injected-into";

    /// <summary>
    /// What a factory registration resolves, as a list of <see cref="ServiceKey"/>. A factory is a closed box to
    /// validation and the dependency graph; a registration that carries this entry is treated like a constructor with
    /// those parameters instead. The source generator adds it to the registrations it writes, and you can add it to
    /// your own factories with <c>WithMetadata(ServiceMetadata.Dependencies, new[] { ServiceKey.For&lt;IClock&gt;() })</c>.
    /// </summary>
    public const string Dependencies = "maxen.dependencies";

    /// <summary>
    /// Used as the key of an entry in <see cref="Dependencies"/> to say "whatever key this registration itself has",
    /// which is what a parameter marked <c>[FromKeyedServices]</c> without a key asks for.
    /// </summary>
    public static readonly object SameKey = new SameKeyMarker();

    private sealed class SameKeyMarker
    {
        public override string ToString() => "(same key)";
    }
}
