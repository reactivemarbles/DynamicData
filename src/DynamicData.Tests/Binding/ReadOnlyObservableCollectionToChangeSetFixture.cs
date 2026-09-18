#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Binding;

public class ReadOnlyObservableCollectionToChangeSetFixture : IDisposable
{
    private readonly TestObservableCollection<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    private readonly ReadOnlyObservableCollection<int> _target;

    public ReadOnlyObservableCollectionToChangeSetFixture()
    {
        _collection = new TestObservableCollection<int>();
        _target = new ReadOnlyObservableCollection<int>(_collection);
        _results = _target.ToObservableChangeSet().AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        _collection.Add(1);

        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(1);
    }

    public void Dispose() => _results.Dispose();

    [Test]
    public async Task Duplicates()
    {
        _collection.Add(1);
        _collection.Add(1);

        await Assert.That(_results.Data.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Move()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        await Assert.That(_results.Data.Items).IsEquivalentTo(_target);
        _collection.Move(5, 8);
        await Assert.That(_results.Data.Items).IsEquivalentTo(_target);

        _collection.Move(7, 1);
        await Assert.That(_results.Data.Items).IsEquivalentTo(_target);
    }

    [Test]
    public async Task RefreshNotSupported()
    {
        // Arrange
        var sourceCache = new SourceCache<Item, Guid>(item => item.Id);

        var item1 = new Item("Old Name");

        sourceCache.AddOrUpdate(item1);

        var sourceCacheResults = sourceCache.Connect().AutoRefresh(item => item.Name).Bind(out var collection).AsAggregator();

        var collectionResults = collection.ToObservableChangeSet().AsAggregator();

        item1.Name = "New Name";

        // Source cache received add and refresh
        await Assert.That(sourceCacheResults.Messages.Count).IsEqualTo(2);
        await Assert.That(sourceCacheResults.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(sourceCacheResults.Messages.Last().Refreshes).IsEqualTo(1);

        // Collection only receives add and NOT refresh
        // System.Collections.Specialized.NotifyCollectionChangedAction does not have an enum to describe the same item being refreshed.
        // https://docs.microsoft.com/en-us/dotnet/api/system.collections.specialized.notifycollectionchangedaction
        await Assert.That(collectionResults.Messages.Count).IsEqualTo(1);
        await Assert.That(collectionResults.Messages.First().Adds).IsEqualTo(1);

        sourceCache.Dispose();
        sourceCacheResults.Dispose();
        collectionResults.Dispose();
    }

    [Test]
    public async Task Remove()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        _collection.Remove(3);

        await Assert.That(_results.Data.Count).IsEqualTo(9);
        await Assert.That(_results.Data.Items.Contains(3)).IsFalse();
        await Assert.That(_results.Data.Items).IsEquivalentTo(_target);
    }

    [Test]
    public async Task Replace()
    {
        _collection.AddRange(Enumerable.Range(1, 10));
        _collection[8] = 20;

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 20, 10 });
    }

    [Test]
    public async Task ResetFiresClearsAndAdds()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        _collection.Reset();
        await Assert.That(_results.Data.Items).IsEquivalentTo(_target);

        var resetNotification = _results.Messages.Last();
        await Assert.That(resetNotification.Removes).IsEqualTo(10);
        await Assert.That(resetNotification.Adds).IsEqualTo(10);
    }

    private class TestObservableCollection<T> : ObservableCollection<T>
    {
        public void Reset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
