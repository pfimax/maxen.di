using System.Collections.Concurrent;
using System.Text;

namespace Maxen.DI.Diagnostics;

/// <summary>
/// An observer that simply remembers what happened. Handy in tests and when you're asking
/// yourself "why did that get built, and what did it drag in?".
/// </summary>
public sealed class ResolutionTrace : IResolutionObserver
{
    private readonly ConcurrentQueue<ResolutionEvent> _events = new();

    /// <summary>Everything recorded so far, parents before their dependencies.</summary>
    public IReadOnlyList<ResolutionEvent> Events => _events.OrderBy(e => e.Sequence).ToList();

    /// <inheritdoc/>
    public void OnResolved(ResolutionEvent resolution) => _events.Enqueue(resolution);

    /// <summary>Forgets everything recorded so far.</summary>
    public void Clear() => _events.Clear();

    /// <summary>The trace as an indented tree with timings.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        foreach (var e in Events)
        {
            text.Append(' ', e.Depth * 2)
                .Append(e.Key)
                .Append(" [").Append(e.Lifetime).Append("] ")
                .Append(e.Duration.TotalMilliseconds.ToString("F3")).Append(" ms");

            if (e.Error is not null) text.Append(" FAILED: ").Append(e.Error.GetType().Name);
            text.AppendLine();
        }
        return text.ToString();
    }
}
