using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// One call like <c>container.Resolve&lt;T&gt;()</c> made on the root container (not a scope), remembered with
    /// the service type it asked for so the analyzer can later check how that service is registered.
    /// </summary>
    internal sealed class RootResolve
    {
        public RootResolve(ITypeSymbol service, Location location)
        {
            Service = service;
            Location = location;
        }

        public ITypeSymbol Service { get; }

        public Location Location { get; }
    }
}
