using Maxen.DI.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Specification;

namespace Maxen.DI.Specification.Tests;

/// <summary>
/// Microsoft's own conformance suite for <see cref="IServiceProvider"/> implementations, run against the Maxen.DI adapter.
/// Passing it is the strongest evidence that code written for the Microsoft container behaves the same here.
/// </summary>
public class MaxenSpecificationTests : DependencyInjectionSpecificationTests
{
    /// <inheritdoc/>
    protected override IServiceProvider CreateServiceProvider(IServiceCollection serviceCollection)
        // Microsoft's default provider does not validate scopes, and the suite resolves scoped services from the root.
        // Maxen.DI validates by default (a deliberate, stricter choice), so the suite switches it off to compare like with like.
        => serviceCollection.BuildMaxenServiceProvider(builder => builder.Options.ValidateScopes = false);
}
