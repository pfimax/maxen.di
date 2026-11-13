using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// Works out a <see cref="ResolvePlan"/> by reading constructors and registrations, never by building anything.
/// Every "not sure" answer is a refusal: a missing plan only costs speed, a wrong one would change behaviour.
/// </summary>
internal static class ResolvePlanner
{
    // A plan is a shortcut, so it isn't worth any price. One that can't share its steps (see Planned.Shareable)
    // doubles with every level of a graph whose services are reached by two paths; past this many steps the planner
    // gives up and the normal path builds the service.
    private const int MaxNodes = 2000;

    // One step on the way down: the service and the container that owns it.
    private readonly record struct PathEntry(Container Owner, ServiceDescriptor Descriptor);

    // Thrown to back out of a deep plan as soon as one dependency can't be planned; caught in Create.
    private sealed class Refusal : Exception { }

    // The lifetimes found at or below a step, counting only services whose container validates scopes. It is what a
    // step that was planned earlier has to be checked against when another path reaches it.
    [Flags]
    private enum LifetimeSet
    {
        None = 0,
        Transient = 1,
        Scoped = 2,
        Singleton = 4,
    }

    /// <summary>
    /// What planning one service gave. <paramref name="Shareable"/> is false when the step, or one below it, depends on
    /// the path that led to it: a factory is told who is asking, so it needs a step of its own for every path.
    /// </summary>
    private readonly record struct Planned(PlanNode Node, LifetimeSet Lifetimes, bool Shareable);

    // Everything one Create call carries around while it walks the graph.
    private sealed class Session
    {
        public readonly List<PathEntry> Path = new();

        // Steps already planned, by registration, so a service many others depend on is planned once and not once
        // per path. Without this the plan of a graph with shared services grows with the number of paths through it.
        public readonly Dictionary<ServiceDescriptor, Planned> Shared = new(ReferenceEqualityComparer.Instance);

        public int Nodes;

        public void Count()
        {
            if (++Nodes > MaxNodes) throw new Refusal();
        }
    }

    public static ResolvePlan Create(Container container, ServiceDescriptor descriptor)
    {
        if (!container.CanUsePlans) return ResolvePlan.None;

        try
        {
            var session = new Session();
            var root = Plan(container, descriptor, session);
            var plan = new ResolvePlan(root.Node, session.Nodes);

            // A plain registration can skip the pipeline for repeat resolves too; the pipeline normally sets these.
            descriptor.CanShortcutCachedInstance = ReferenceEquals(descriptor.Lifetime, Lifetime.Singleton);
            descriptor.CanShortcutScopedInstance = ReferenceEquals(descriptor.Lifetime, Lifetime.Scoped);
            return plan;
        }
        catch (Exception)
        {
            // A refusal, or anything odd while reading registrations (a broken source, say): the normal path
            // builds the service and reports whatever is wrong.
            return ResolvePlan.None;
        }
    }

    // Collections, Lazy<T> and Func<T> are the framework-made services a plan understands; anything else synthetic is
    // left to the normal path. Lazy and Func cost nothing to plan because they resolve later, from a captured scope.
    private static Planned PlanSynthetic(Container owner, ServiceKey key, ServiceDescriptor descriptor, Session session)
    {
        if (descriptor.Factory is null) throw new Refusal();

        if (DeferredPlanNode.TryDescribe(key, out var inner, out var isLazy))
        {
            session.Count();
            return new Planned(new DeferredPlanNode(key.Type, inner, isLazy), LifetimeSet.None, Shareable: true);
        }

        var element = CollectionSource.ElementType(key.Type);
        if (element is null) throw new Refusal();

        session.Count();

        var lifetimes = LifetimeSet.None;
        var shareable = true;
        var usesScoped = false;
        var items = new List<PlanNode>();

        foreach (var (itemOwner, itemDescriptor) in owner.FindAllOwned(new ServiceKey(element, key.Key)))
        {
            var item = Plan(itemOwner, itemDescriptor, session);
            items.Add(item.Node);
            lifetimes |= item.Lifetimes;
            shareable &= item.Shareable;
            usesScoped |= item.Node.UsesScoped;
        }

        return new Planned(new CollectionPlanNode(element, items.ToArray()) { UsesScoped = usesScoped }, lifetimes, shareable);
    }

    private static Planned Plan(Container owner, ServiceDescriptor descriptor, Session session)
    {
        var path = session.Path;
        var lifetime = descriptor.Lifetime;
        var builtIn = ReferenceEquals(lifetime, Lifetime.Transient)
            || ReferenceEquals(lifetime, Lifetime.Scoped)
            || ReferenceEquals(lifetime, Lifetime.Singleton);

        if (!builtIn
            || descriptor.Condition is not null
            || descriptor.IsSynthetic
            || descriptor.HasMiddleware
            || descriptor.IsOpenGeneric)
            throw new Refusal();

        // Loops and captive dependencies are left to the pipeline, which knows how to explain them.
        foreach (var entry in path)
            if (ReferenceEquals(entry.Descriptor, descriptor)) throw new Refusal();

        if (session.Shared.TryGetValue(descriptor, out var known))
        {
            // Planned on another path already. Nothing about the step itself depends on how we got here, but the
            // lifetime rule does: the services above it on this path are different ones, and each of them must be
            // allowed to hold everything below.
            foreach (var ancestor in path)
                if (!CanHold(ancestor.Descriptor.Lifetime, known.Lifetimes)) throw new Refusal();
            return known;
        }

        var validates = owner.Options.ValidateScopes;
        if (validates)
            foreach (var ancestor in path)
                if (!ancestor.Descriptor.Lifetime.CanDependOn(lifetime)) throw new Refusal();

        session.Count();

        var lifetimes = validates ? SetOf(lifetime) : LifetimeSet.None;
        var usesScoped = ReferenceEquals(lifetime, Lifetime.Scoped);

        // A factory is a leaf as far as the plan can see. It runs with a context whose parents are the services above it,
        // so a loop or captive dependency created through the factory is still caught by the normal checks. Those
        // parents are this path's, which is why the step can't be handed to another path.
        if (descriptor.Factory is not null)
        {
            var ancestors = path.Select(e => (e.Owner, e.Descriptor)).ToArray();
            var factory = new ConstructedPlanNode(descriptor, owner, null, Array.Empty<PlanNode?>(), Array.Empty<object?>(), ancestors) { UsesScoped = usesScoped };
            return new Planned(factory, lifetimes, Shareable: false);
        }

        if (descriptor.Instance is not null)
        {
            var instance = new ConstructedPlanNode(descriptor, owner, null, Array.Empty<PlanNode?>(), Array.Empty<object?>()) { UsesScoped = false };
            return Share(session, descriptor, new Planned(instance, lifetimes, Shareable: true));
        }

        var constructor = owner.Factory.PlanWithoutBuilding(descriptor.ImplementationType!, owner)
            ?? throw new Refusal();

        var dependencies = new PlanNode?[constructor.Parameters.Length];
        var defaults = new object?[dependencies.Length];
        var shareable = true;

        path.Add(new PathEntry(owner, descriptor));
        for (var i = 0; i < dependencies.Length; i++)
        {
            var key = constructor.Keys[i];
            var found = owner.FindOwnerAndDescriptor(key);

            if (found is null)
            {
                // Not registered: the normal path would use the parameter's default value, or fail.
                if (!constructor.Parameters[i].HasDefaultValue) throw new Refusal();
                defaults[i] = DefaultParameterResolver.DefaultOf(constructor.Parameters[i]);
                continue;
            }

            var (foundOwner, foundDescriptor) = found.Value;
            var child = foundDescriptor.IsSynthetic
                ? PlanSynthetic(owner, key, foundDescriptor, session)
                : Plan(foundOwner, foundDescriptor, session);
            dependencies[i] = child.Node;
            usesScoped |= child.Node.UsesScoped;
            lifetimes |= child.Lifetimes;
            shareable &= child.Shareable;
        }
        path.RemoveAt(path.Count - 1);

        var node = new ConstructedPlanNode(descriptor, owner, constructor, dependencies, defaults) { UsesScoped = usesScoped };
        var planned = new Planned(node, lifetimes, shareable);
        return shareable ? Share(session, descriptor, planned) : planned;
    }

    private static Planned Share(Session session, ServiceDescriptor descriptor, Planned planned)
    {
        session.Shared[descriptor] = planned;
        return planned;
    }

    // Only the three built-in lifetimes reach a plan, so a small set of flags covers every case.
    private static LifetimeSet SetOf(Lifetime lifetime)
        => ReferenceEquals(lifetime, Lifetime.Scoped) ? LifetimeSet.Scoped
            : ReferenceEquals(lifetime, Lifetime.Singleton) ? LifetimeSet.Singleton
            : LifetimeSet.Transient;

    /// <summary>The lifetime rule for a whole set at once: may a service with <paramref name="holder"/> depend on all of them?</summary>
    private static bool CanHold(Lifetime holder, LifetimeSet below)
        => ((below & LifetimeSet.Transient) == 0 || holder.CanDependOn(Lifetime.Transient))
            && ((below & LifetimeSet.Scoped) == 0 || holder.CanDependOn(Lifetime.Scoped))
            && ((below & LifetimeSet.Singleton) == 0 || holder.CanDependOn(Lifetime.Singleton));
}
