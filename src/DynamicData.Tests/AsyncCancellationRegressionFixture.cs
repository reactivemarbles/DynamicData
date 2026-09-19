namespace DynamicData.Tests;

public class AsyncCancellationRegressionFixture
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CacheTransformDisposalCancelsFactoryWithoutNotifyingObserver(bool handleErrors)
    {
        using var source = new SourceCache<int, int>(value => value);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;
        Func<int, ReactiveUI.Primitives.Optional<int>, int, CancellationToken, Task<string>> factory =
            async (value, previous, key, cancellationToken) =>
            {
                started.TrySetResult(cancellationToken);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return value.ToString();
                }
                catch (OperationCanceledException)
                {
                    cancelled.TrySetResult();
                    throw;
                }
            };
        var changes = handleErrors
            ? source.Connect().TransformSafeAsync(factory, _ => Interlocked.Increment(ref notifications), new TransformAsyncOptions())
            : source.Connect().TransformAsync(factory, new TransformAsyncOptions());
        using var subscription = changes.Subscribe(
            _ => Interlocked.Increment(ref notifications),
            _ => Interlocked.Increment(ref notifications),
            () => Interlocked.Increment(ref notifications));

        source.AddOrUpdate(1);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(token.CanBeCanceled).IsTrue();
        await Assert.That(token.IsCancellationRequested).IsTrue();
        await Assert.That(Volatile.Read(ref notifications)).IsEqualTo(0);
    }

    [Test]
    public async Task ListTransformDisposalCancelsFactoryWithoutNotifyingObserver()
    {
        using var source = new SourceList<int>();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;
        Func<int, ReactiveUI.Primitives.Optional<string>, int, CancellationToken, Task<string>> factory =
            async (value, previous, index, cancellationToken) =>
            {
                started.TrySetResult(cancellationToken);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return value.ToString();
                }
                catch (OperationCanceledException)
                {
                    cancelled.TrySetResult();
                    throw;
                }
            };
        using var subscription = source.Connect().TransformAsync(factory).Subscribe(
            _ => Interlocked.Increment(ref notifications),
            _ => Interlocked.Increment(ref notifications),
            () => Interlocked.Increment(ref notifications));

        source.Add(1);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(token.CanBeCanceled).IsTrue();
        await Assert.That(token.IsCancellationRequested).IsTrue();
        await Assert.That(Volatile.Read(ref notifications)).IsEqualTo(0);
    }
}
