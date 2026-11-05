using System.Runtime.CompilerServices;

namespace Maxen.DI.Async;

/// <summary>Makes sure each <see cref="IAsyncInitializable"/> instance is initialized once, however many callers ask.</summary>
internal static class AsyncInitializer
{
    // Weak, so remembering an instance never keeps it alive.
    private static readonly ConditionalWeakTable<object, Task> Started = new();

    public static Task EnsureInitializedAsync(object instance, CancellationToken cancellationToken)
    {
        if (instance is not IAsyncInitializable initializable)
            return Task.CompletedTask;

        lock (Started)
        {
            // Everyone who asks while initialization is running gets the same task and waits for it.
            if (Started.TryGetValue(instance, out var existing))
                return existing;

            var task = initializable.InitializeAsync(cancellationToken);
            Started.Add(instance, task);

            // A failed or cancelled attempt must not poison the instance forever: forget it so the next caller can retry.
            task.ContinueWith(
                _ => { lock (Started) Started.Remove(instance); },
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return task;
        }
    }
}
