namespace DynamicData.Tests.List;

public class SortChangedFixture
{
    private static readonly IComparer<ListItem> DefaultComparer = SortExpressionComparer<ListItem>.Ascending(x => x.Number);


    /// <summary>
    /// See https://github.com/reactivemarbles/DynamicData/issues/473
    /// </summary>
    [Fact]
    public void SortsWithoutError()
    {
        var source = new SourceList<ListItem>();
        var sorter = new Subject<IComparer<ListItem>>();

        source.AddRange(Enumerable.Range(1, 10).Select(i => new ListItem(i)));

        source.Connect()
            .Sort(sorter)
            .Bind(out var bound)
            .Subscribe();

        bound.Select(x => x.Number).Should().BeInAscendingOrder();

        sorter.OnNext(SortExpressionComparer<ListItem>.Descending(x => x.Number));


        bound.Select(x => x.Number).Should().BeInDescendingOrder();
    }


    private class ListItem(int number) : IComparable<ListItem>
    {
        public int Number { get; } = number;

        public int CompareTo(ListItem? other) => DefaultComparer.Compare(this, other);
            
    }
}

public class SortFixture : IDisposable
{
    private const int Seed = 0x5047_1167;

    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

    private readonly RandomPersonGenerator _generator = new();

    private readonly Randomizer _randomizer = new(Seed);

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public SortFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().Sort(_comparer).AsAggregator();
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void Insert()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbefirst = new Person("__A", 99);
        _source.Add(shouldbefirst);

        _results.Data.Count.Should().Be(101, "Should be 100 people in the cache");

        _results.Data.Items[0].Should().Be(shouldbefirst);
    }

    [Fact]
    public void InsertSortingImmediatelyBeforeAJustSortedRange_EmitsChangesThatReplayToTheSortedList()
    {
        // The edit produces [AddRange, Add] without any source-side merging. Sorting places the range at the end and the
        // single item one slot before the range, so only the sort's own change tracking decides the emitted indexes.
        var values = new SortedSet<int>();
        while (values.Count < 5)
        {
            values.Add(_randomizer.Int());
        }

        if (values.ToArray() is not [var lowest, var inserted, var middle, var rangeFirst, var rangeSecond])
        {
            throw new InvalidOperationException("Expected exactly five unique values.");
        }

        using var source = new SourceList<int>();
        source.AddRange([middle, lowest]);

        using var results = source.Connect().Sort(Comparer<int>.Default).AsAggregator();

        source.Edit(list =>
        {
            list.AddRange([rangeFirst, rangeSecond]);
            list.Insert(0, inserted);
        });

        results.Data.Items.Should().Equal(lowest, inserted, middle, rangeFirst, rangeSecond);
    }

    [Fact]
    public void Remove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var toRemove = people.ElementAt(20);
        people.RemoveAt(20);
        _source.RemoveAt(20);

        _results.Data.Count.Should().Be(99, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
        _results.Messages[1].First().Item.Current.Should().Be(toRemove, "Incorrect item removed");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyOdds()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var odd = people.Select((p, idx) => new { p, idx }).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

        _source.RemoveMany(odd);

        _results.Data.Count.Should().Be(50, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

        _results.Data.Count.Should().Be(10, "Should be 10 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyReverseOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

        _results.Data.Count.Should().Be(10, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Replace()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbefirst = new Person("__A", 99);
        _source.ReplaceAt(10, shouldbefirst);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        _results.Data.Items[0].Should().Be(shouldbefirst);
    }

    [Fact]
    public void SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}
