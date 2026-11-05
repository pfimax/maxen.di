namespace Maxen.DI.Pipeline;

/// <summary>
/// Remembers "wrap services of this type with that decorator", optionally limited by a predicate.
/// A lower <paramref name="Priority"/> sits closer to the real service; equal priorities keep registration order.
/// </summary>
internal sealed record DecoratorRegistration(Type ServiceType, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type DecoratorType, Func<ServiceDescriptor, bool>? Predicate, int Priority = 0)
{
    public bool Matches(ServiceDescriptor descriptor)
    {
        var type = descriptor.Key.Type;

        // An open registration like IRepo<> also covers every closed form (IRepo<Customer>, ...).
        var typeMatches = type == ServiceType
            || (ServiceType.IsGenericTypeDefinition
                && type.IsConstructedGenericType
                && type.GetGenericTypeDefinition() == ServiceType);

        return typeMatches && (Predicate?.Invoke(descriptor) ?? true);
    }
}
