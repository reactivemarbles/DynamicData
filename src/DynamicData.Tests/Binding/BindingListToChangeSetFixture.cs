#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Binding;

public class BindingListToChangeSetFixture : IDisposable
{
    private readonly TestBindingList<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    public BindingListToChangeSetFixture()
    {
        _collection = new TestBindingList<int>();
        _results = _collection.ToObservableChangeSet().AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        _collection.Add(1);

        await Assert.That(_results.Messages.Count).IsEqualTo(1);
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
    public async Task RaiseListChangedEvents()
    {
        _collection.RaiseListChangedEvents = true;
        _collection.Add(1);

        await Assert.That(_results.Messages.Count).IsEqualTo(1);

        _collection.RaiseListChangedEvents = false;
        _collection.Add(1);

        await Assert.That(_results.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshCausesReplace()
    {
        // Arrange
        var sourceCache = new SourceCache<Item, Guid>(item => item.Id);

        var item1 = new Item("Old Name");

        sourceCache.AddOrUpdate(item1);

        var collection = new TestBindingList<Item>();

        var sourceCacheResults = sourceCache.Connect().AutoRefresh(item => item.Name).Bind(collection).AsAggregator();

        var collectionResults = collection.ToObservableChangeSet().AsAggregator();

        item1.Name = "New Name";

        // Source cache received add and refresh
        await Assert.That(sourceCacheResults.Messages.Count).IsEqualTo(2);
        await Assert.That(sourceCacheResults.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(sourceCacheResults.Messages.Last().Refreshes).IsEqualTo(1);

        /*
             List receives add and replace instead of refresh (and as of 23/02/2023 it receives a refresh too!)
         */

        await Assert.That(collectionResults.Messages.Count).IsEqualTo(3);
        await Assert.That(collectionResults.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(collectionResults.Messages.First().Refreshes).IsEqualTo(0);
        await Assert.That(collectionResults.Messages.Last().Replaced).IsEqualTo(1);
        await Assert.That(collectionResults.Messages.Last().Refreshes).IsEqualTo(0);

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
        await Assert.That(_results.Data.Items).IsEquivalentTo(_collection);
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
        await Assert.That(_results.Data.Items).IsEquivalentTo(_collection);

        var resetNotification = _results.Messages.Last();
        await Assert.That(resetNotification.Removes).IsEqualTo(10);
        await Assert.That(resetNotification.Adds).IsEqualTo(10);
    }

    [Test]
    public async Task InsertInto()
    {
        //Fixes https://github.com/reactivemarbles/DynamicData/issues/507
        var target = new ObservableCollectionExtended<string>();

        var bindingList = new BindingList<string>() { "a", "b", "c", "d" };
        bindingList.ToObservableChangeSet()
            .Bind(target)
            .Subscribe();

        bindingList.Insert(2, "Z at index 2");

        await Assert.That(target).IsEquivalentTo(new[] { "a", "b", "Z at index 2", "c", "d" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private class TestBindingList<T> : BindingList<T>
    {
        public void Reset() => OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }
}
