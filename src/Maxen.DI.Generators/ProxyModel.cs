namespace Maxen.DI.Generators
{
    /// <summary>
    /// The generated proxy for one interface: its name (for ordering) and the source of the class body, written
    /// with the placeholder <c>{CLASS}</c> where the emitter puts the final class name. Plain strings, so unchanged
    /// interfaces don't trigger regeneration.
    /// </summary>
    internal sealed record ProxyModel(string SortKey, string InterfaceName, string ClassBody);
}
