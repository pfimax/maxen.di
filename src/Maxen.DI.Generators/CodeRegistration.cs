using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// One <c>builder.Register&lt;...&gt;().AsXxx()</c> chain the analyzer found in code: what was registered,
    /// how long it lives, and where to point the squiggle if something is wrong with it.
    /// </summary>
    internal sealed class CodeRegistration
    {
        public CodeRegistration(
            INamedTypeSymbol service, INamedTypeSymbol implementation, string lifetime, bool isSpecific, Location location,
            ILocalSymbol? builder)
        {
            Service = service;
            Implementation = implementation;
            Lifetime = lifetime;
            IsSpecific = isSpecific;
            Location = location;
            Builder = builder;
        }

        public INamedTypeSymbol Service { get; }
        public INamedTypeSymbol Implementation { get; }

        /// <summary>"Singleton", "Scoped" or "Transient".</summary>
        public string Lifetime { get; }

        /// <summary>
        /// True for keyed or conditional registrations. They only apply to some requests, so the analyzer
        /// neither trusts them as "the" registration of a service nor guesses which one a constructor gets.
        /// </summary>
        public bool IsSpecific { get; }

        public Location Location { get; }

        /// <summary>
        /// The local variable holding the builder this was registered on, or null when the call was made on anything
        /// else (a parameter, a field, the result of a call). See <see cref="RegistrationFacts.IsOwnContainer"/>.
        /// </summary>
        public ILocalSymbol? Builder { get; }
    }
}
