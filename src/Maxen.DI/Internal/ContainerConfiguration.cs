using Maxen.DI.Pipeline;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// A snapshot of the extension setup a container was built with. Child containers start from
/// their parent's snapshot, so middleware, decorators, initializers and so on keep applying.
/// </summary>
internal sealed class ContainerConfiguration
{
    public required IReadOnlyList<IRegistrationSource> Sources { get; init; }
    public required IReadOnlyList<IResolutionMiddleware> PreMiddleware { get; init; }
    public required IReadOnlyList<IResolutionMiddleware> PostMiddleware { get; init; }
    public required IReadOnlyList<IParameterResolver> ParameterResolvers { get; init; }
    public required IReadOnlyList<IInstanceInitializer> Initializers { get; init; }
    public required IReadOnlyList<DecoratorRegistration> Decorators { get; init; }
    public required IConstructorSelector ConstructorSelector { get; init; }
}
