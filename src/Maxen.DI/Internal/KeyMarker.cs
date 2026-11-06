namespace Maxen.DI.Internal;

/// <summary>
/// A stand-in placed where a service key would go, for keys that are not a real value: "any key", or "whatever key
/// the service being built has". Compared by reference only, so it can never collide with a key someone registered.
/// </summary>
internal sealed class KeyMarker
{
    private readonly string _description;

    public KeyMarker(string description) => _description = description;

    // Shows up in error messages and graphs, so it should read as a phrase, not a type name.
    public override string ToString() => _description;
}
