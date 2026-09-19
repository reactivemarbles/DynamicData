#if REACTIVE_TESTS
using DynamicData.Reactive;
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

namespace DynamicData.Tests.Cache;

public class FactoryEdgeCoverageFixture
{
    [Test]
    public async Task CacheTaskFactoryPublishesWhileDelegateIsAwaitingAndResumesAfterGate()
    {
        var allowReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int, int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int, int>(
            async cache =>
            {
                try
                {
                    cache.AddOrUpdate(42);
                    await allowReturn.Task;
                }
                finally
                {
                    exited.TrySetResult();
                }
            },
            static value => value);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        try
        {
            var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(changeSet.Single().Current).IsEqualTo(42);
        }
        finally
        {
            allowReturn.TrySetResult();
        }

        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CacheTaskFactoryPropagatesErrorsAfterPublishingPendingEdits()
    {
        var error = new InvalidOperationException("cache task failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int, int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int, int>(
            async cache =>
            {
                try
                {
                    cache.AddOrUpdate(7);
                    await allowError.Task;
                    throw error;
                }
                finally
                {
                    exited.TrySetResult();
                }
            },
            static value => value);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), failure => receivedError.TrySetResult(failure));

        try
        {
            var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(changeSet.Single().Current).IsEqualTo(7);
        }
        finally
        {
            allowError.TrySetResult();
        }

        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(observed).IsSameReferenceAs(error);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CacheCancellableTaskFactoryPropagatesErrorsAfterPublishingPendingEdits()
    {
        var error = new InvalidOperationException("cache cancellable task failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int, int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int, int>(
            async (cache, _) =>
            {
                try
                {
                    cache.AddOrUpdate(9);
                    await allowError.Task;
                    throw error;
                }
                finally
                {
                    exited.TrySetResult();
                }
            },
            static value => value);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), failure => receivedError.TrySetResult(failure));

        try
        {
            var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(changeSet.Single().Current).IsEqualTo(9);
        }
        finally
        {
            allowError.TrySetResult();
        }

        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(observed).IsSameReferenceAs(error);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListDisposableFactoryPublishesItemsAndDisposesReturnedResource()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            list =>
            {
                list.AddRange(new[] { 1, 2 });
                return Disposable.Create(() => disposed.TrySetResult());
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 1, 2 });

        subscription.Dispose();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListDisposableFactoryPropagatesSubscribeErrors()
    {
        var error = new InvalidOperationException("list disposable failed");
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<ISourceList<int>, IDisposable> subscribe =
            list =>
            {
                list.Add(1);
                throw error;
            };
        var changes = ObservableChangeSet.Create(subscribe);

        using var subscription = changes.Subscribe(_ => { }, failure => receivedError.TrySetResult(failure));

        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(observed).IsSameReferenceAs(error);
    }

    [Test]
    public async Task ListTaskDisposableFactoryWrapperPublishesItemsAndDisposesReturnedResource()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async list =>
            {
                list.AddRange(new[] { 3, 4 });
                await Task.Yield();
                return Disposable.Create(() => disposed.TrySetResult());
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 3, 4 });

        subscription.Dispose();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListTaskActionFactoryWrapperPublishesItemsAndRunsReturnedCleanup()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async list =>
            {
                list.AddRange(new[] { 5, 6 });
                await Task.Yield();
                return () => disposed.TrySetResult();
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 5, 6 });

        subscription.Dispose();
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListTaskFactoryPublishesWhileDelegateIsAwaitingAndPropagatesErrors()
    {
        var error = new InvalidOperationException("list task failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async list =>
            {
                try
                {
                    list.AddRange(new[] { 7, 8 });
                    await allowError.Task;
                    throw error;
                }
                finally
                {
                    exited.TrySetResult();
                }
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), failure => receivedError.TrySetResult(failure));

        try
        {
            var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 7, 8 });
        }
        finally
        {
            allowError.TrySetResult();
        }

        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(observed).IsSameReferenceAs(error);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListCancellableTaskFactoryPublishesOnlyAfterSuccessfulDelegateCompletion()
    {
        var allowReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async (list, _) =>
            {
                try
                {
                    list.AddRange(new[] { 9, 10 });
                    await allowReturn.Task;
                }
                finally
                {
                    exited.TrySetResult();
                }
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        try
        {
            await Assert.That(received.Task.IsCompleted).IsFalse();
        }
        finally
        {
            allowReturn.TrySetResult();
        }

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 9, 10 });
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListCancellableTaskFactoryPropagatesErrorsWithoutPublishingPendingEdits()
    {
        var error = new InvalidOperationException("list cancellable task failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async (list, _) =>
            {
                try
                {
                    list.AddRange(new[] { 11, 12 });
                    await allowError.Task;
                    throw error;
                }
                finally
                {
                    exited.TrySetResult();
                }
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), failure => receivedError.TrySetResult(failure));

        allowError.TrySetResult();
        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(observed).IsSameReferenceAs(error);
        await Assert.That(received.Task.IsCompleted).IsFalse();
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CacheUpdaterUsesListFastPathAndIteratorPathForKeyValuePairs()
    {
        var cache = new ChangeAwareCache<string, int>();
        var updater = new CacheUpdater<string, int>(cache);

        updater.AddOrUpdate(new List<KeyValuePair<int, string>>
        {
            new(1, "one"),
            new(2, "two")
        });
        updater.AddOrUpdate(IteratorPairs());

        await Assert.That(cache.Lookup(1).Value).IsEqualTo("one");
        await Assert.That(cache.Lookup(2).Value).IsEqualTo("two");
        await Assert.That(cache.Lookup(3).Value).IsEqualTo("three");
    }

    [Test]
    public async Task CacheUpdaterUsesListFastPathAndIteratorPathForRefreshAndRemoveKeys()
    {
        var cache = new ChangeAwareCache<string, int>();
        var updater = new CacheUpdater<string, int>(cache, static value => value.Length);

        updater.AddOrUpdate(new[] { "aa", "bbb", "cccc" });
        cache.CaptureChanges();

        updater.Refresh(new List<int> { 2 });
        updater.Refresh(IteratorKeys(3));
        updater.Remove(new List<int> { 2 });
        updater.Remove(IteratorKeys(3));

        var changes = cache.CaptureChanges();

        await Assert.That(cache.Lookup(4).Value).IsEqualTo("cccc");
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Refresh)).IsEqualTo(2);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Remove)).IsEqualTo(2);
    }

    private static IEnumerable<int> GetCurrentItems(IChangeSet<int> changeSet) =>
        changeSet.SelectMany(static change => change.Range.Count == 0 ? Enumerable.Repeat(change.Item.Current, 1) : change.Range);

    private static IEnumerable<KeyValuePair<int, string>> IteratorPairs()
    {
        yield return new KeyValuePair<int, string>(3, "three");
    }

    private static IEnumerable<int> IteratorKeys(int key)
    {
        yield return key;
    }
}
