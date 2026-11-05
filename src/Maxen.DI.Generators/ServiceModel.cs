namespace Maxen.DI.Generators
{
    /// <summary>
    /// What we learned about one [Service] class: either the line of code that registers it, or the
    /// reason we couldn't. Only strings and records, so equal inputs compare equal and Roslyn can skip regenerating.
    /// </summary>
    internal sealed record ServiceModel(string SortKey, string? Statement, DiagnosticModel? Diagnostic, ServiceInfo? Info = null);
}
