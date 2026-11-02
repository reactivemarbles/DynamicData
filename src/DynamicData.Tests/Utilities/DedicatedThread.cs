namespace DynamicData.Tests.Utilities;

/// <summary>
/// Runs work on a thread of its own instead of a thread-pool thread. Use it for any thread a concurrency test blocks,
/// at a <see cref="Barrier"/> or inside a parked delivery: a blocked pool thread can starve the pool, leaving the
/// threads the test is waiting for unstarted.
/// </summary>
internal static class DedicatedThread
{
    /// <summary>Runs <paramref name="action"/> on a new dedicated thread.</summary>
    /// <param name="action">The work to run.</param>
    /// <returns>A task that completes when <paramref name="action"/> has run.</returns>
    public static Task Run(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
