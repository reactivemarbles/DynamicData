#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.List;

public class CloneChangesFixture
{
    private readonly List<int> _clone;

    private readonly ChangeAwareList<int> _source;

    public CloneChangesFixture()
    {
        _source = new ChangeAwareList<int>();
        _clone = new List<int>();
    }

    [Test]
    public async Task Add()
    {
        _source.Add(1);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);

        //assert collection
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task AddManyInSuccession()
    {
        Enumerable.Range(1, 10).ForEach(_source.Add);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task AddRange()
    {
        _source.AddRange(Enumerable.Range(1, 10));

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task AddSecond()
    {
        _source.Add(1);
        _source.Add(2);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task AddSecondRange()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.AddRange(Enumerable.Range(11, 10));
        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task InsertRangeInCentre()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.InsertRange(Enumerable.Range(11, 10), 5);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task MovedItemInListHigherToLowerIsMoved()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.Move(2, 1);

        var changes = _source.CaptureChanges();

        _clone.Clone(changes);

        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task MovedItemInListLowerToHigherIsMoved()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.Move(1, 2);

        var changes = _source.CaptureChanges();

        _clone.Clone(changes);

        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task MovedItemInObservableCollectionIsMoved()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.Move(1, 2);

        var clone = new ObservableCollection<int>();
        var changes = _source.CaptureChanges();
        var itemMoved = false;

        clone.CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                itemMoved = true;
            }
        };

        clone.Clone(changes);

        await Assert.That(itemMoved).IsTrue();
    }

    [Test]
    public async Task Remove()
    {
        _source.Add(1);
        _source.Remove(1);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveInnerRange()
    {
        _source.AddRange(Enumerable.Range(1, 10));

        _source.RemoveRange(5, 3);
        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveMany()
    {
        _source.AddRange(Enumerable.Range(1, 10));

        _source.RemoveMany(Enumerable.Range(1, 10));
        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveManyPartial()
    {
        _source.AddRange(Enumerable.Range(1, 10));

        _source.RemoveMany(Enumerable.Range(3, 5));
        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveRange()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.RemoveRange(5, 3);

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveSucession()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.ClearChanges();

        _source.ToArray().ForEach(i => _source.Remove(i));
        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }

    [Test]
    public async Task RemoveSucessionReversed()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _source.ClearChanges();

        _source.OrderByDescending(i => i).ToArray().ForEach(i => _source.Remove(i));

        var changes = _source.CaptureChanges();
        _clone.Clone(changes);
        await Assert.That(_clone).IsEquivalentTo(_source);
    }
}
