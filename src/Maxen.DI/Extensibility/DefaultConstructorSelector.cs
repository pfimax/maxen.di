using Maxen.DI.Internal;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// The usual "greediest constructor wins" rule: take the constructor with the most parameters
/// that the container can fully satisfy. A constructor marked <see cref="InjectAttribute"/> is
/// preferred over all others.
/// </summary>
public sealed class DefaultConstructorSelector : IConstructorSelector
{
    /// <inheritdoc/>
    public ConstructorInfo Select(Type implementationType, IReadOnlyList<ConstructorInfo> constructors, Func<ParameterInfo, bool> canResolve)
    {
        var name = TypeNames.Format(implementationType);
        if (constructors.Count == 0)
            throw new ResolutionException($"'{name}' has no public constructor the container can call.");

        // An explicit [Inject] is the developer telling us which one to use.
        var marked = constructors.Where(c => c.IsDefined(typeof(InjectAttribute), false)).ToList();
        var pool = marked.Count > 0 ? (IReadOnlyList<ConstructorInfo>)marked : constructors;

        var usable = pool
            .Where(c => c.GetParameters().All(canResolve))
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        if (usable.Count == 0)
        {
            // Point at the constructor that is closest to working, that's the most useful hint.
            var closest = pool.OrderBy(c => c.GetParameters().Count(p => !canResolve(p))).First();
            var missing = closest.GetParameters()
                .Where(p => !canResolve(p))
                .Select(p => $"{TypeNames.Format(p.ParameterType)} {p.Name}");

            throw new ResolutionException(
                $"Unable to select a constructor for '{name}'. Unresolvable parameters: {string.Join(", ", missing)}.");
        }

        // Two equally greedy constructors means we'd be guessing, so ask the developer to decide.
        if (usable.Count > 1 && usable[0].GetParameters().Length == usable[1].GetParameters().Length)
            throw new ResolutionException(
                $"Ambiguous constructors for '{name}': several constructors with {usable[0].GetParameters().Length} " +
                "resolvable parameters. Mark one with [Inject].");

        return usable[0];
    }
}
