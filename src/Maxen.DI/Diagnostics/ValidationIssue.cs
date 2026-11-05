namespace Maxen.DI.Diagnostics;

/// <summary>One problem found while validating the container.</summary>
/// <param name="Kind">What sort of problem it is.</param>
/// <param name="Service">The registration the problem belongs to.</param>
/// <param name="Message">A human-readable explanation, including the dependency path where relevant.</param>
public sealed record ValidationIssue(ValidationIssueKind Kind, ServiceKey Service, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"[{Kind}] {Message}";
}
