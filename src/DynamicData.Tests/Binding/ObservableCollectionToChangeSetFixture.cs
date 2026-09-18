#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Binding;

public class ObservableCollectionToChangeSetFixture : IDisposable
{
    private readonly TestObservableCollection<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    public ObservableCollectionToChangeSetFixture()
    {
        _collection = new TestObservableCollection<int>();
        _results = _collection.ToObservableChangeSet().AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        _collection.Add(1);

        await Assert.That(_results.Messages.Count).IsGreaterThanOrEqualTo(1);
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

        await Assert.That(_results.Data.Items).IsEquivalentTo(_collection);
        _collection.Move(5, 8);
        await Assert.That(_results.Data.Items).IsEquivalentTo(_collection);

        _collection.Move(7, 1);
        await Assert.That(_results.Data.Items).IsEquivalentTo(_collection);
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

    private class TestObservableCollection<T> : ObservableCollection<T>
    {
        public void Reset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
