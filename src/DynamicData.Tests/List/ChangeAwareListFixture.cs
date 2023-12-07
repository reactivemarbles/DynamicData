using System;
using System.Linq;

using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class ChangeAwareListFixture
{
    private readonly ChangeAwareList<int> _list;

    public ChangeAwareListFixture() => _list = new ChangeAwareList<int>();

    [Fact]
    public void Add()
    {
        _list.Add(1);

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Adds.Should().Be(1);
        changes.First().Item.Current.Should().Be(1);

        //assert collection
        _list.Should().BeEquivalentTo(Enumerable.Range(1, 1));
    }

    [Fact]
    public void AddManyInSuccession()
    {
        Enumerable.Range(1, 10).ForEach(_list.Add);

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Adds.Should().Be(10);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        //assert collection
        _list.Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }

    [Fact]
    public void AddRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Adds.Should().Be(10);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        _list.Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }

    [Fact]
    public void AddSecond()
    {
        _list.Add(1);
        _list.ClearChanges();

        _list.Add(2);

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Adds.Should().Be(1);
        changes.First().Item.Current.Should().Be(2);
        //assert collection
        _list.Should().BeEquivalentTo(Enumerable.Range(1, 2));
    }

    [Fact]
    public void AddSecondRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.AddRange(Enumerable.Range(11, 10));
        var changes = _list.CaptureChanges();

        //assert changes
        changes.Count.Should().Be(2);
        changes.Adds.Should().Be(20);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        changes.Skip(1).First().Range.Should().BeEquivalentTo(Enumerable.Range(11, 10));

        //assert collection
        _list.Should().BeEquivalentTo(Enumerable.Range(1, 20));
    }

    [Fact]
    public void InsertRangeInCentre()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.InsertRange(Enumerable.Range(11, 10), 5);
        var changes = _list.CaptureChanges();

        //assert changes
        changes.Count.Should().Be(2);
        changes.Adds.Should().Be(20);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        changes.Skip(1).First().Range.Should().BeEquivalentTo(Enumerable.Range(11, 10));

        var shouldBe = Enumerable.Range(1, 5).Union(Enumerable.Range(11, 10)).Union(Enumerable.Range(6, 5));
        //assert collection
        _list.Should().BeEquivalentTo(shouldBe);
    }

    [Fact]
    public void Refresh()
    {
        _list.AddRange(Enumerable.Range(0, 9));
        _list.ClearChanges();
        _list.Refresh(1);

        //assert changes (should batch)
        var changes = _list.CaptureChanges();

        changes.Count.Should().Be(1);
        changes.Refreshes.Should().Be(1);
        changes.First().Reason.Should().Be(ListChangeReason.Refresh);
        changes.First().Item.Current.Should().Be(1);

        _list.Refresh(5).Should().Be(true);
        _list.Refresh(-1).Should().Be(false);
        _list.Refresh(1000).Should().Be(false);
    }

    [Fact]
    public void RefreshAt()
    {
        _list.AddRange(Enumerable.Range(0, 9));
        _list.ClearChanges();
        _list.RefreshAt(1);

        //assert changes (should batch)
        var changes = _list.CaptureChanges();

        changes.Count.Should().Be(1);
        changes.Refreshes.Should().Be(1);
        changes.First().Reason.Should().Be(ListChangeReason.Refresh);
        changes.First().Item.Current.Should().Be(1);

        Assert.Throws<ArgumentException>(() => _list.RefreshAt(-1));
        Assert.Throws<ArgumentException>(() => _list.RefreshAt(1000));
    }

    [Fact]
    public void Remove()
    {
        _list.Add(1);
        _list.ClearChanges();

        _list.Remove(1);

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Removes.Should().Be(1);
        changes.First().Item.Current.Should().Be(1);
        //assert collection
        _list.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveMany()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.RemoveMany(Enumerable.Range(1, 10));

        //assert changes (should batch)s
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Removes.Should().Be(10);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        _list.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveRange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.RemoveRange(5, 3);

        //assert changes
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Removes.Should().Be(3);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(6, 3));

        //assert collection
        var shouldBe = Enumerable.Range(1, 5).Union(Enumerable.Range(9, 2));
        //assert collection
        _list.Should().BeEquivalentTo(shouldBe);
    }

    [Fact]
    public void RemoveSucession()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.ToArray().ForEach(i => _list.Remove(i));

        //assert changes (should batch)s
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Removes.Should().Be(10);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));

        //assert collection
        _list.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveSucessionReversed()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.ClearChanges();

        _list.OrderByDescending(i => i).ToArray().ForEach(i => _list.Remove(i));

        //assert changes (should batch)
        var changes = _list.CaptureChanges();
        changes.Count.Should().Be(1);
        changes.Removes.Should().Be(10);
        changes.First().Range.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        //assert collection
        _list.Count.Should().Be(0);
    }

    [Fact]
    public void ThrowWhenRemovingItemOutsideOfBoundaries() => Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveAt(0));

    [Fact]
    public void ThrowWhenRemovingRangeThatBeginsOutsideOfBoundaries() => Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveRange(0, 1));

    [Fact]
    public void ThrowWhenRemovingRangeThatFinishesOutsideOfBoundaries()
    {
        _list.Add(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveRange(0, 2));
    }
}
