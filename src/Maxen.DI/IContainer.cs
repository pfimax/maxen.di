namespace Maxen.DI;

/// <summary>The root scope. It owns the singletons and carries the container-wide configuration.</summary>
public interface IContainer : IScope
{
    /// <summary>The strictness switches this container was built with.</summary>
    ContainerOptions Options { get; }
}
