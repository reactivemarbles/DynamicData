namespace DynamicData.Tests.Cache;

public class AutoRefreshConcurrencyRegressionFixture
{
    [Test]
    public async Task DisposedChildNotificationAfterRemoveIsIgnored()
    {
        using var source = new TestSourceCache<Item, int>(static item => item.Id);

        var item = new Item(1, "Initial");
        var refreshes = new ManualRefreshObservable<Unit>();

        source.AddOrUpdate(item);

        using var subscription = source.Connect()
            .AutoRefreshOnObservable((_, _) => refreshes)
            .ValidateSynchronization()
            .ValidateChangeSets(static item => item.Id)
            .RecordCacheItems(out var results);

        source.Remove(item);

        refreshes.OnNext(Unit.Default);

        await Assert.That(refreshes.DisposeCount).IsEqualTo(1);
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(2);
        await Assert.That(results.RecordedChangeSets[1].Removes).IsEqualTo(1);
        await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty();
    }

    [Test]
    public async Task DisposedChildNotificationAfterReplaceIsIgnoredForSameKey()
    {
        using var source = new TestSourceCache<Item, int>(static item => item.Id);

        var oldItem = new Item(1, "Old");
        var newItem = new Item(1, "New");
        var oldRefreshes = new ManualRefreshObservable<Unit>();
        var newRefreshes = new ManualRefreshObservable<Unit>();

        source.AddOrUpdate(oldItem);

        using var subscription = source.Connect()
            .AutoRefreshOnObservable((item, _) => ReferenceEquals(item, oldItem) ? oldRefreshes : newRefreshes)
            .ValidateSynchronization()
            .ValidateChangeSets(static item => item.Id)
            .RecordCacheItems(out var results);

        source.AddOrUpdate(newItem);
        oldRefreshes.OnNext(Unit.Default);
        newRefreshes.OnNext(Unit.Default);

        await Assert.That(oldRefreshes.DisposeCount).IsEqualTo(1);
        await Assert.That(newRefreshes.DisposeCount).IsEqualTo(0);
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(3);
        await Assert.That(results.RecordedChangeSets[1].Updates).IsEqualTo(1);
        await Assert.That(results.RecordedChangeSets[2].Refreshes).IsEqualTo(1);
        await Assert.That(results.RecordedChangeSets[2].Single().Current).IsSameReferenceAs(newItem);
    }

    private sealed record Item(int Id, string Name);

    private sealed class ManualRefreshObservable<T> : IObservable<T>
    {
        private IObserver<T>? _observer;
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            return new Subscription(this);
        }

        public void OnNext(T value)
            => _observer?.OnNext(value);

        private sealed class Subscription(ManualRefreshObservable<T> owner) : IDisposable
        {
            public void Dispose()
                => Interlocked.Increment(ref owner._disposeCount);
        }
    }
}
