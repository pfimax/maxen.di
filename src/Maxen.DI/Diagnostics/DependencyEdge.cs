namespace Maxen.DI.Diagnostics;

/// <summary>"<paramref name="From"/> needs <paramref name="To"/>" in the dependency graph.</summary>
/// <param name="From">The service whose constructor has the parameter.</param>
/// <param name="To">The registration that satisfies it.</param>
/// <param name="Deferred">
/// True for <c>Lazy&lt;T&gt;</c> and <c>Func&lt;T&gt;</c>: the dependency is only resolved later, so it
/// can't cause a construction loop or capture a shorter-lived service while building.
/// </param>
public sealed record DependencyEdge(ServiceDescriptor From, ServiceDescriptor To, bool Deferred);
