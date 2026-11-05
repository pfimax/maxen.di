namespace Maxen.DI.Samples.Features.Validation;

/// <summary>A singleton that wrongly takes a scoped service: the instance would outlive the request it was built in.</summary>
internal sealed class AuditLog
{
    public AuditLog(CurrentUser user) => User = user;

    public CurrentUser User { get; }
}
