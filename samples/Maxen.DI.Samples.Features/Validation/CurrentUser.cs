namespace Maxen.DI.Samples.Features.Validation;

/// <summary>Lives for one request, so a singleton must not hold on to it.</summary>
internal sealed class CurrentUser
{
}
