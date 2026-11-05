namespace Maxen.DI;

/// <summary>
/// Marks a class as a decorator of <see cref="ServiceType"/>: every registration of that service gets wrapped in it,
/// exactly as <c>builder.Decorate(serviceType, decoratorType)</c> would. The decorator takes the service it wraps as a
/// constructor parameter. Picked up by <c>RegisterAttributed</c> and by the source generator, like <see cref="ServiceAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DecoratorAttribute : Attribute
{
    /// <summary>Declares the class a decorator of <paramref name="serviceType"/>.</summary>
    public DecoratorAttribute(Type serviceType) => ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

    /// <summary>The service this class wraps.</summary>
    public Type ServiceType { get; }

    /// <summary>Lower numbers sit closer to the real service. Decorators with the same priority stack in registration order.</summary>
    public int Priority { get; set; }
}
