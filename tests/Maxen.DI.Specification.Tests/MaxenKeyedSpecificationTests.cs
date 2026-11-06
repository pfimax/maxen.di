using Maxen.DI.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Specification;

namespace Maxen.DI.Specification.Tests;

/// <summary>
/// Microsoft's conformance suite for keyed services, run against the Maxen.DI adapter.
/// The main suite leaves keyed services out entirely, so without this one "passes the conformance suite"
/// would say nothing about <c>[FromKeyedServices]</c>, <c>[ServiceKey]</c> or <c>KeyedService.AnyKey</c>.
/// </summary>
public class MaxenKeyedSpecificationTests : KeyedDependencyInjectionSpecificationTests
{
    /// <inheritdoc/>
    protected override IServiceProvider CreateServiceProvider(IServiceCollection collection)
        // Same reasoning as the main suite: it resolves scoped services from the root, which Maxen.DI refuses by default.
        => collection.BuildMaxenServiceProvider(builder => builder.Options.ValidateScopes = false);
}
