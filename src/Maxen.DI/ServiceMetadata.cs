namespace Maxen.DI;

/// <summary>Names of the metadata entries the framework itself understands.</summary>
public static class ServiceMetadata
{
    /// <summary>Marks a registration whose constructor takes some values at resolve time.</summary>
    public const string RuntimeArguments = "maxen.runtime-arguments";

    /// <summary>The consumer type a <c>WhenInjectedInto</c> registration is limited to. Lets validation spot ones that can never match.</summary>
    public const string InjectedInto = "maxen.injected-into";
}
