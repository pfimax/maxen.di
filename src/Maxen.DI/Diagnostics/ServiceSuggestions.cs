using Maxen.DI.Internal;

namespace Maxen.DI.Diagnostics;

/// <summary>
/// Looks through what is registered for something the caller probably meant, so a "not registered"
/// error can say "did you mean ...?" instead of leaving them to hunt for a typo or a missing key.
/// </summary>
internal static class ServiceSuggestions
{
    private const int MaxSuggestions = 3;

    public static IReadOnlyList<string> For(ServiceKey wanted, IEnumerable<ServiceDescriptor> registered)
    {
        var suggestions = new List<string>();

        foreach (var descriptor in registered)
        {
            var reason = Explain(wanted, descriptor);
            if (reason is not null && !suggestions.Contains(reason))
                suggestions.Add(reason);

            if (suggestions.Count == MaxSuggestions) break;
        }
        return suggestions;
    }

    private static string? Explain(ServiceKey wanted, ServiceDescriptor descriptor)
    {
        var have = descriptor.Key;

        // Same service, but registered under a key (or without one) that the caller didn't use.
        if (have.Type == wanted.Type)
            return have.Key is null
                ? $"'{have}' is registered without a key"
                : $"'{have}' is registered with key '{have.Key}'";

        // They asked for the class, but only its interface was registered.
        if (descriptor.ImplementationType == wanted.Type && Equals(have.Key, wanted.Key))
            return $"'{have}' is registered and implemented by '{TypeNames.Format(wanted.Type)}'; resolve that instead";

        // Same short name in another namespace, or a near miss such as a typo.
        if (Equals(have.Key, wanted.Key) && Similar(have.Type.Name, wanted.Type.Name))
            return $"'{have}' is registered";

        return null;
    }

    private static bool Similar(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        // Very short names are "close" to almost everything, so don't guess for those.
        if (Math.Min(a.Length, b.Length) < 5) return false;

        return EditDistance(a.ToLowerInvariant(), b.ToLowerInvariant()) <= 2;
    }

    /// <summary>Classic Levenshtein distance: how many single-character edits turn one string into the other.</summary>
    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
