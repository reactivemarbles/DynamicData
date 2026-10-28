namespace DynamicData.Tests.List;

public class ChangeAwareListFixture
{
    private const int Seed = 0x0C4A_0617;
    private const int MaxRangeSize = 5;

    private readonly ChangeAwareList<int> _list;
    private readonly Randomizer _randomizer = new(Seed);

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

    [Fact]
    public void InsertImmediatelyBeforeAnAppendedRange_ChangesReplayToTheList()
    {
        var list = CreateCapturedList(_randomizer.Int(1, MaxRangeSize), out var replica);

        list.AddRange(NextValues(_randomizer.Int(1, MaxRangeSize)));
        list.Insert(replica.Count - 1, _randomizer.Int());

        replica.Clone(list.CaptureChanges());
        replica.Should().Equal(list);
    }

    [Fact]
    public void InsertAtAnyIndexAfterAPendingInsertRange_ChangesReplayToTheList()
    {
        var initialCount = _randomizer.Int(2, MaxRangeSize);
        var rangeIndex = _randomizer.Int(1, initialCount - 1);
        var range = NextValues(_randomizer.Int(1, MaxRangeSize));
        var item = _randomizer.Int();

        for (var index = 0; index <= initialCount + range.Length; index++)
        {
            var list = CreateCapturedList(initialCount, out var replica);

            list.InsertRange(range, rangeIndex);
            list.Insert(index, item);

            replica.Clone(list.CaptureChanges());
            replica.Should().Equal(list, "inserting at index {0} after a range inserted at {1} must replay correctly", index, rangeIndex);
        }
    }

    [Fact]
    public void InsertIntoASeededListBeforeCapture_ChangesReplayToTheList()
    {
        var list = new ChangeAwareList<int>(NextValues(_randomizer.Int(1, MaxRangeSize)));
        var replica = new List<int>();

        list.Insert(_randomizer.Int(0, list.Count - 1), _randomizer.Int());

        replica.Clone(list.CaptureChanges());
        replica.Should().Equal(list);
    }

    [Fact]
    public void RemoveAtAnyIndexAfterAPendingRemove_ChangesReplayToTheList()
    {
        var initialCount = _randomizer.Int(3, MaxRangeSize * 2);
        var firstIndex = _randomizer.Int(1, initialCount - 2);

        for (var index = 0; index < initialCount - 1; index++)
        {
            var list = CreateCapturedList(initialCount, out var replica);

            list.RemoveAt(firstIndex);
            list.RemoveAt(index);

            replica.Clone(list.CaptureChanges());
            replica.Should().Equal(list, "removing at index {0} after removing at {1} must replay correctly", index, firstIndex);
        }
    }

    [Fact]
    public void RemoveAtAnyIndexAfterAPendingRemoveRange_ChangesReplayToTheList()
    {
        var initialCount = _randomizer.Int(MaxRangeSize + 2, MaxRangeSize * 3);
        var rangeCount = _randomizer.Int(2, MaxRangeSize);
        var rangeIndex = _randomizer.Int(1, initialCount - rangeCount - 1);

        for (var index = 0; index < initialCount - rangeCount; index++)
        {
            var list = CreateCapturedList(initialCount, out var replica);

            list.RemoveRange(rangeIndex, rangeCount);
            list.RemoveAt(index);

            replica.Clone(list.CaptureChanges());
            replica.Should().Equal(list, "removing at index {0} after removing a range at {1} must replay correctly", index, rangeIndex);
        }
    }

    [Fact]
    public void RandomEditBatches_ChangesReplayToTheList()
    {
        const int batchCount = 2_000;
        const int maxOperationsPerBatch = 12;

        var replica = new List<int>();

        for (var batch = 0; batch < batchCount; batch++)
        {
            var operations = new List<string>();
            var operationCount = _randomizer.Int(1, maxOperationsPerBatch);

            for (var i = 0; i < operationCount; i++)
            {
                operations.Add(ApplyRandomOperation(_list));
            }

            replica.Clone(_list.CaptureChanges());
            replica.Should().Equal(_list, "batch {0} [{1}] must replay correctly", batch, string.Join(", ", operations));
        }
    }

    private ChangeAwareList<int> CreateCapturedList(int count, out List<int> replica)
    {
        var list = new ChangeAwareList<int>();
        list.AddRange(NextValues(count));
        list.CaptureChanges();

        replica = [.. list];
        return list;
    }

    private int[] NextValues(int count) => Enumerable.Range(0, count).Select(_ => _randomizer.Int()).ToArray();

    private string ApplyRandomOperation(ChangeAwareList<int> list)
    {
        var count = list.Count;
        var operation = count == 0 ? _randomizer.Int(0, 3) : _randomizer.Int(0, 9);

        switch (operation)
        {
            case 0:
                list.Add(_randomizer.Int());
                return "Add";

            case 1:
                {
                    var index = _randomizer.Int(0, count);
                    list.Insert(index, _randomizer.Int());
                    return $"Insert({index})";
                }

            case 2:
                {
                    var values = NextValues(_randomizer.Int(1, MaxRangeSize));
                    list.AddRange(values);
                    return $"AddRange({values.Length})";
                }

            case 3:
                {
                    var index = _randomizer.Int(0, count);
                    var values = NextValues(_randomizer.Int(1, MaxRangeSize));
                    list.InsertRange(values, index);
                    return $"InsertRange({index}, {values.Length})";
                }

            case 4:
                {
                    var index = _randomizer.Int(0, count - 1);
                    list.Remove(list[index]);
                    return $"Remove(item@{index})";
                }

            case 5:
                {
                    var index = _randomizer.Int(0, count - 1);
                    list.RemoveAt(index);
                    return $"RemoveAt({index})";
                }

            case 6:
                {
                    var index = _randomizer.Int(0, count - 1);
                    var rangeCount = _randomizer.Int(1, count - index);
                    list.RemoveRange(index, rangeCount);
                    return $"RemoveRange({index}, {rangeCount})";
                }

            case 7:
                {
                    var original = _randomizer.Int(0, count - 1);
                    var destination = _randomizer.Int(0, count - 1);
                    list.Move(original, destination);
                    return $"Move({original}, {destination})";
                }

            case 8:
                {
                    var index = _randomizer.Int(0, count - 1);
                    list[index] = _randomizer.Int();
                    return $"Set({index})";
                }

            default:
                list.Clear();
                return "Clear";
        }
    }
}
