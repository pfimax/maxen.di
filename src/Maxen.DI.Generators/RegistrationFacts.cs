using System.Collections.Concurrent;
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

        /// <summary>Every <c>Register&lt;...&gt;()</c> chain with a readable lifetime.</summary>
        public ConcurrentBag<CodeRegistration> Registrations { get; } = new ConcurrentBag<CodeRegistration>();

        /// <summary>Resolve calls made directly on the root container.</summary>
        public ConcurrentBag<RootResolve> RootResolves { get; } = new ConcurrentBag<RootResolve>();

        /// <summary>Service types registered by factory, instance, replace, [Service] and similar calls.</summary>
        public ConcurrentBag<ITypeSymbol> OtherServices { get; } = new ConcurrentBag<ITypeSymbol>();

        /// <summary>
        /// True once we have seen scanning, modules, generated registrations or anything else that registers
        /// services we cannot list. A "missing registration" verdict is only safe when this is still false.
        /// </summary>
        public bool HasOpaqueRegistrations => Volatile.Read(ref _opaque) != 0;

        public void MarkOpaque() => Interlocked.Exchange(ref _opaque, 1);
    }
}
