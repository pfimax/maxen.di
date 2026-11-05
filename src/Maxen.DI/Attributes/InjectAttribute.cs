namespace Maxen.DI;

/// <summary>
/// On a constructor: "use this one". On a property: "fill this in after construction"
/// (requires <c>UsePropertyInjection()</c>). On a method: "call this after construction with these
/// services" (requires <c>UseMethodInjection()</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class InjectAttribute : Attribute
{
    /// <summary>For properties only. When false, a missing service leaves the property alone instead of throwing.</summary>
    public bool Required { get; set; } = true;
}
