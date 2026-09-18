#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.List;

public class ChangeAwareListFixture
{
    private readonly ChangeAwareList<int> _list;

    public ChangeAwareListFixture() => _list = new ChangeAwareList<int>();

    [Test]
    public async Task Add()
    {
        _list.Add(1);

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Adds).IsEqualTo(1);
        await Assert.That(changes.First().Item.Current).IsEqualTo(1);

        //assert collection
        await Assert.That(_list).IsEquivalentTo(Enumerable.Range(1, 1));
    }

    [Test]
    public async Task AddManyInSuccession()
    {
        Enumerable.Range(1, 10).ForEach(_list.Add);

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Adds).IsEqualTo(10);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));
        //assert collection
        await Assert.That(_list).IsEquivalentTo(Enumerable.Range(1, 10));
    }

    [Test]
    public async Task AddRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Adds).IsEqualTo(10);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        await Assert.That(_list).IsEquivalentTo(Enumerable.Range(1, 10));
    }

    [Test]
    public async Task AddSecond()
    {
        _list.Add(1);
        _list.ClearChanges();

        _list.Add(2);

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Adds).IsEqualTo(1);
        await Assert.That(changes.First().Item.Current).IsEqualTo(2);
        //assert collection
        await Assert.That(_list).IsEquivalentTo(Enumerable.Range(1, 2));
    }

    [Test]
    public async Task AddSecondRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.AddRange(Enumerable.Range(11, 10));
        var changes = _list.CaptureChanges();

        //assert changes
        await Assert.That(changes.Count).IsEqualTo(2);
        await Assert.That(changes.Adds).IsEqualTo(20);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));
        await Assert.That(changes.Skip(1).First().Range).IsEquivalentTo(Enumerable.Range(11, 10));

        //assert collection
        await Assert.That(_list).IsEquivalentTo(Enumerable.Range(1, 20));
    }

    [Test]
    public async Task InsertRangeInCentre()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.InsertRange(Enumerable.Range(11, 10), 5);
        var changes = _list.CaptureChanges();

        //assert changes
        await Assert.That(changes.Count).IsEqualTo(2);
        await Assert.That(changes.Adds).IsEqualTo(20);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));
        await Assert.That(changes.Skip(1).First().Range).IsEquivalentTo(Enumerable.Range(11, 10));

        var shouldBe = Enumerable.Range(1, 5).Union(Enumerable.Range(11, 10)).Union(Enumerable.Range(6, 5));
        //assert collection
        await Assert.That(_list).IsEquivalentTo(shouldBe);
    }

    [Test]
    public async Task Refresh()
    {
        _list.AddRange(Enumerable.Range(0, 9));
        _list.ClearChanges();
        _list.Refresh(1);

        //assert changes (should batch)
        var changes = _list.CaptureChanges();

        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Refreshes).IsEqualTo(1);
        await Assert.That(changes.First().Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(changes.First().Item.Current).IsEqualTo(1);

        await Assert.That(_list.Refresh(5)).IsTrue();
        await Assert.That(_list.Refresh(-1)).IsFalse();
        await Assert.That(_list.Refresh(1000)).IsFalse();
    }

    [Test]
    public async Task RefreshAt()
    {
        _list.AddRange(Enumerable.Range(0, 9));
        _list.ClearChanges();
        _list.RefreshAt(1);

        //assert changes (should batch)
        var changes = _list.CaptureChanges();

        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Refreshes).IsEqualTo(1);
        await Assert.That(changes.First().Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(changes.First().Item.Current).IsEqualTo(1);

        await Assert.That(() => _list.RefreshAt(-1)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => _list.RefreshAt(1000)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Remove()
    {
        _list.Add(1);
        _list.ClearChanges();

        _list.Remove(1);

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Removes).IsEqualTo(1);
        await Assert.That(changes.First().Item.Current).IsEqualTo(1);
        //assert collection
        await Assert.That(_list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveMany()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.RemoveMany(Enumerable.Range(1, 10));

        //assert changes (should batch)s
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Removes).IsEqualTo(10);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        await Assert.That(_list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.RemoveRange(5, 3);

        //assert changes
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Removes).IsEqualTo(3);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(6, 3));

        //assert collection
        var shouldBe = Enumerable.Range(1, 5).Union(Enumerable.Range(9, 2));
        //assert collection
        await Assert.That(_list).IsEquivalentTo(shouldBe);
    }

    [Test]
    public async Task RemoveSucession()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.ToArray().ForEach(i => _list.Remove(i));

        //assert changes (should batch)s
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Removes).IsEqualTo(10);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        await Assert.That(_list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveSucessionReversed()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.OrderByDescending(i => i).ToArray().ForEach(i => _list.Remove(i));

        //assert changes (should batch)
        var changes = _list.CaptureChanges();
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes.Removes).IsEqualTo(10);
        await Assert.That(changes.First().Range).IsEquivalentTo(Enumerable.Range(1, 10));
        //assert collection
        await Assert.That(_list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ThrowWhenRemovingItemOutsideOfBoundaries() => await Assert.That(() => _list.RemoveAt(0)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task ThrowWhenRemovingRangeThatBeginsOutsideOfBoundaries() => await Assert.That(() => _list.RemoveRange(0, 1)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task ThrowWhenRemovingRangeThatFinishesOutsideOfBoundaries()
    {
        _list.Add(0);
        await Assert.That(() => _list.RemoveRange(0, 2)).ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
