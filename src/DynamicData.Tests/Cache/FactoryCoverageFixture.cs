#if REACTIVE_TESTS
using DynamicData.Reactive;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class FactoryCoverageFixture
{
    [Test]
    public async Task CacheActionFactoryPublishesItemsAndRunsDisposeAction()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<Person, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new Person("Alpha", 1);
        var second = new Person("Beta", 2);

        var changes = ObservableChangeSet.Create<Person, string>(
            cache =>
            {
                cache.AddOrUpdate(new[] { first, second });
                return () => disposed.TrySetResult();
            },
            static person => person.Name);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var itemsByKey = changeSet.ToDictionary(static change => change.Key, static change => change.Current);

        await Assert.That(itemsByKey[first.Name]).IsSameReferenceAs(first);
        await Assert.That(itemsByKey[second.Name]).IsSameReferenceAs(second);

        subscription.Dispose();

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CacheAsyncDisposableFactoryPublishesItemsAndDisposesReturnedResource()
    {
        var allowReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<Person, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var person = new Person("Async", 3);

        var changes = ObservableChangeSet.Create<Person, string>(
            async (cache, _) =>
            {
                cache.AddOrUpdate(person);
                await allowReturn.Task;
                returned.TrySetResult();
                return Disposable.Create(() => disposed.TrySetResult());
            },
            static item => item.Name);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));
        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(changeSet.Single().Current).IsSameReferenceAs(person);

        allowReturn.SetResult();
        await returned.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task CacheAsyncActionFactoryPropagatesErrors()
    {
        var error = new InvalidOperationException("cache failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<Person, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<Person, string>(
            async (cache, _) =>
            {
                cache.AddOrUpdate(new Person("BeforeError", 4));
                await allowError.Task;
                throw error;
            },
            static item => item.Name);

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), error => receivedError.TrySetResult(error));
        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(changeSet.Single().Key).IsEqualTo("BeforeError");

        allowError.SetResult();
        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(observed).IsSameReferenceAs(error);
    }

    [Test]
    public async Task CacheAsyncFactoryDisposalCancelsToken()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;

        var changes = ObservableChangeSet.Create<Person, string>(
            async (cache, token) =>
            {
                try
                {
                    started.TrySetResult(token);
                    using var registration = token.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), cancelled);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    cache.AddOrUpdate(new Person("Never", 5));
                }
                finally
                {
                    exited.TrySetResult();
                }
            },
            static item => item.Name);

        using var subscription = changes.Subscribe(
            _ => Interlocked.Increment(ref notifications),
            _ => Interlocked.Increment(ref notifications),
            () => Interlocked.Increment(ref notifications));

        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(token.CanBeCanceled).IsTrue();
        await Assert.That(token.IsCancellationRequested).IsTrue();
        await Assert.That(Volatile.Read(ref notifications)).IsEqualTo(0);
    }

    [Test]
    public async Task ListActionFactoryPublishesItemsAndRunsDisposeAction()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            list =>
            {
                list.AddRange(new[] { 1, 2, 3 });
                return () => disposed.TrySetResult();
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));

        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 1, 2, 3 });

        subscription.Dispose();

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListAsyncDisposableFactoryPublishesItemsAndDisposesReturnedResource()
    {
        var allowReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async (list, _) =>
            {
                list.AddRange(new[] { 7, 8 });
                await allowReturn.Task;
                returned.TrySetResult();
                return Disposable.Create(() => disposed.TrySetResult());
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value));
        var changeSet = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(GetCurrentItems(changeSet)).IsEquivalentTo(new[] { 7, 8 });

        allowReturn.SetResult();
        await returned.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();

        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ListAsyncActionFactoryPropagatesErrors()
    {
        var error = new InvalidOperationException("list failed");
        var allowError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<IChangeSet<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changes = ObservableChangeSet.Create<int>(
            async (list, _) =>
            {
                list.AddRange(new[] { 11, 12 });
                await allowError.Task;
                throw error;
            });

        using var subscription = changes.Subscribe(value => received.TrySetResult(value), error => receivedError.TrySetResult(error));

        allowError.SetResult();
        var observed = await receivedError.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(observed).IsSameReferenceAs(error);
        await Assert.That(received.Task.IsCompleted).IsFalse().Because("the list async factory does not publish pending list edits before the async delegate terminates with an error");
    }

    [Test]
    public async Task ListAsyncFactoryDisposalCancelsToken()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;

        var changes = ObservableChangeSet.Create<int>(
            async (list, token) =>
            {
                try
                {
                    started.TrySetResult(token);
                    using var registration = token.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), cancelled);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    list.Add(1);
                }
                finally
                {
                    exited.TrySetResult();
                }
            });

        using var subscription = changes.Subscribe(
            _ => Interlocked.Increment(ref notifications),
            _ => Interlocked.Increment(ref notifications),
            () => Interlocked.Increment(ref notifications));

        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        subscription.Dispose();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(token.CanBeCanceled).IsTrue();
        await Assert.That(token.IsCancellationRequested).IsTrue();
        await Assert.That(Volatile.Read(ref notifications)).IsEqualTo(0);
    }

    private static IEnumerable<int> GetCurrentItems(IChangeSet<int> changeSet) =>
        changeSet.SelectMany(static change => change.Range.Count == 0 ? Enumerable.Repeat(change.Item.Current, 1) : change.Range);
}
