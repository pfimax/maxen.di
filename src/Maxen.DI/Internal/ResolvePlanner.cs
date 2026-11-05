using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// Works out a <see cref="ResolvePlan"/> by reading constructors and registrations, never by building anything.
/// Every "not sure" answer is a refusal: a missing plan only costs speed, a wrong one would change behaviour.
/// </summary>
internal static class ResolvePlanner
{
    // One step on the way down: the service and the container that owns it.
    private readonly record struct PathEntry(Container Owner, ServiceDescriptor Descriptor);

    // Thrown to back out of a deep plan as soon as one dependency can't be planned; caught in Create.
    private sealed class Refusal : Exception { }

    public static ResolvePlan Create(Container container, ServiceDescriptor descriptor)
    {
        if (!container.CanUsePlans) return ResolvePlan.None;

        try
        {
            var root = Plan(container, descriptor, new List<PathEntry>());
            var plan = new ResolvePlan(root);

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
    private static PlanNode PlanSynthetic(Container owner, ServiceKey key, ServiceDescriptor descriptor, List<PathEntry> path)
    {
        if (descriptor.Factory is null) throw new Refusal();

        if (DeferredPlanNode.TryDescribe(key, out var inner, out var isLazy))
            return new DeferredPlanNode(key.Type, inner, isLazy);

        var element = CollectionSource.ElementType(key.Type);
        if (element is null) throw new Refusal();

        var items = owner.FindAllOwned(new ServiceKey(element, key.Key))
            .Select(item => Plan(item.Owner, item.Descriptor, path))
            .ToArray();

        return new CollectionPlanNode(element, items) { UsesScoped = items.Any(i => i.UsesScoped) };
    }

    private static PlanNode Plan(Container owner, ServiceDescriptor descriptor, List<PathEntry> path)
    {
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
        if (owner.Options.ValidateScopes)
            foreach (var ancestor in path)
                if (!ancestor.Descriptor.Lifetime.CanDependOn(lifetime)) throw new Refusal();

        var usesScoped = ReferenceEquals(lifetime, Lifetime.Scoped);

        // A factory is a leaf as far as the plan can see. It runs with a context whose parents are the services above it,
        // so a loop or captive dependency created through the factory is still caught by the normal checks.
        if (descriptor.Factory is not null)
            return new ConstructedPlanNode(descriptor, owner, null, Array.Empty<PlanNode?>(), Array.Empty<object?>(), path.Select(e => (e.Owner, e.Descriptor)).ToArray()) { UsesScoped = usesScoped };

        if (descriptor.Instance is not null)
            return new ConstructedPlanNode(descriptor, owner, null, Array.Empty<PlanNode?>(), Array.Empty<object?>()) { UsesScoped = false };

        var constructor = owner.Factory.PlanWithoutBuilding(descriptor.ImplementationType!, owner)
            ?? throw new Refusal();

        var dependencies = new PlanNode?[constructor.Parameters.Length];
        var defaults = new object?[dependencies.Length];

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
                ? PlanSynthetic(owner, key, foundDescriptor, path)
                : Plan(foundOwner, foundDescriptor, path);
            dependencies[i] = child;
            usesScoped |= child.UsesScoped;
        }
        path.RemoveAt(path.Count - 1);

        return new ConstructedPlanNode(descriptor, owner, constructor, dependencies, defaults) { UsesScoped = usesScoped };
    }
}
