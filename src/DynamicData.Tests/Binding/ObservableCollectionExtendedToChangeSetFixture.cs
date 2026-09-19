#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Binding;

public class ObservableCollectionExtendedToChangeSetFixture : IDisposable
{
    private readonly ObservableCollectionExtended<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    private readonly ReadOnlyObservableCollection<int> _target;

    public ObservableCollectionExtendedToChangeSetFixture()
    {
        _collection = new ObservableCollectionExtended<int>();
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

    //[Test]
    //public void ResetFiresClearsAndAdds()
    //{
    //    _collection.AddRange(Enumerable.Range(1, 10));

    //    _collection.Reset();
    //    _results.Data.Items.Should().BeEquivalentTo(_target);

    //    var resetNotification = _results.Messages.Last();
    //    resetNotification.Removes.Should().Be(10);
    //    resetNotification.Adds.Should().Be(10);
    //}

    //private class TestObservableCollection<T> : ObservableCollection<T>
    //{
    //    public void Reset()
    //    {
    //        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    //    }
    //}
}
