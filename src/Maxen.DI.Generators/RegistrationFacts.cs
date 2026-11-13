using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Everything the analyzer learns about registrations while it walks the compilation, gathered from
    /// many threads: the registrations themselves, extra service types registered by other calls, and
    /// whether anything in the code registers services in a way we cannot follow.
    /// </summary>
    internal sealed class RegistrationFacts
    {
        private int _opaque;
        private int _scopeValidationOff;

        /// <summary>
        /// True once the code switches <c>ValidateScopes</c> off somewhere. With it off, a scoped service may be resolved
        /// from the root container, so warning about that would be wrong.
        /// </summary>
        public bool ScopeValidationSwitchedOff => Volatile.Read(ref _scopeValidationOff) != 0;

        /// <summary>Notes an assignment that turns <c>ValidateScopes</c> off, or sets it to something we can't read.</summary>
        public void MarkScopeValidationOff() => Interlocked.Exchange(ref _scopeValidationOff, 1);

        private readonly ConcurrentDictionary<ISymbol, byte> _created = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<ISymbol, byte> _handedAround = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<ISymbol, byte> _opaqueBuilders = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);

        /// <summary>Every <c>Register&lt;...&gt;()</c> chain with a readable lifetime.</summary>
        public ConcurrentBag<CodeRegistration> Registrations { get; } = new ConcurrentBag<CodeRegistration>();

        /// <summary>Resolve calls made directly on the root container.</summary>
        public ConcurrentBag<RootResolve> RootResolves { get; } = new ConcurrentBag<RootResolve>();

        /// <summary>
        /// Service types registered by factory, instance, replace and similar calls, each with the builder variable the
        /// call was made on (null when it wasn't a local variable).
        /// </summary>
        public ConcurrentBag<KeyValuePair<ITypeSymbol, ILocalSymbol?>> OtherServices { get; } = new ConcurrentBag<KeyValuePair<ITypeSymbol, ILocalSymbol?>>();

        /// <summary>Service types of <c>[Service]</c> classes. The generator registers those on whichever builder asks.</summary>
        public ConcurrentBag<ITypeSymbol> AttributedServices { get; } = new ConcurrentBag<ITypeSymbol>();

        /// <summary>
        /// True once we have seen scanning, modules or anything else that registers services we cannot list, on any
        /// builder. A "missing registration" verdict about registrations we can't tie to one container is only safe
        /// while this is still false.
        /// </summary>
        public bool HasOpaqueRegistrations => Volatile.Read(ref _opaque) != 0;

        /// <summary>Notes a call that registers services out of sight, and the builder variable it was made on, if any.</summary>
        public void MarkOpaque(ILocalSymbol? builder = null)
        {
            Interlocked.Exchange(ref _opaque, 1);
            if (builder != null) _opaqueBuilders.TryAdd(builder, 0);
        }

        /// <summary>Notes a local variable that starts life as <c>new ContainerBuilder()</c>, so it begins empty.</summary>
        public void MarkCreated(ILocalSymbol builder) => _created.TryAdd(builder, 0);

        /// <summary>
        /// Notes a builder variable that is used for anything but a call on it: passed to a method, assigned, returned,
        /// captured in a field. From then on other code may add to it, or it may be another builder altogether.
        /// </summary>
        public void MarkHandedAround(ILocalSymbol builder) => _handedAround.TryAdd(builder, 0);

        /// <summary>
        /// True when everything registered on this builder is in plain sight: it was created empty in this method and
        /// never left it. Such a builder is a container of its own, and can be judged by its own registrations alone.
        /// Anything else is judged the cautious way, against every registration in the project.
        /// </summary>
        public bool IsOwnContainer(ILocalSymbol? builder)
            => builder != null && _created.ContainsKey(builder) && !_handedAround.ContainsKey(builder);

        /// <summary>True when a call we cannot follow was made on this builder variable.</summary>
        public bool IsOpaque(ILocalSymbol builder) => _opaqueBuilders.ContainsKey(builder);
    }
}
