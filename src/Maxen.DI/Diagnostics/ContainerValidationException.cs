namespace Maxen.DI.Diagnostics;

/// <summary>Thrown when validation finds problems. Every issue is listed, not just the first.</summary>
public sealed class ContainerValidationException : DIException
{
    /// <summary>Creates the exception from the problems found; the message lists all of them.</summary>
    public ContainerValidationException(IReadOnlyList<ValidationIssue> issues)
        : base(BuildMessage(issues))
        => Issues = issues;

    /// <summary>Every problem validation found.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    private static string BuildMessage(IReadOnlyList<ValidationIssue> issues)
        => $"The container configuration is invalid ({issues.Count} issue(s)):" +
           string.Concat(issues.Select(i => Environment.NewLine + "  - " + i));
}
