#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class SortChangedFixture
{
    private static readonly IComparer<ListItem> DefaultComparer = SortExpressionComparer<ListItem>.Ascending(x => x.Number);

    /// <summary>
    /// See https://github.com/reactivemarbles/DynamicData/issues/473
    /// </summary>
    [Test]
    public async Task SortsWithoutError()
    {
        var source = new SourceList<ListItem>();
        var sorter = new ReactiveUI.Primitives.Signals.Signal<IComparer<ListItem>>();

        source.AddRange(Enumerable.Range(1, 10).Select(i => new ListItem(i)));

        source.Connect()
            .Sort(sorter)
            .Bind(out var bound)
            .Subscribe();

        await Assert.That(bound.Select(x => x.Number)).IsInOrder();

        sorter.OnNext(SortExpressionComparer<ListItem>.Descending(x => x.Number));

        await Assert.That(bound.Select(x => x.Number)).IsInDescendingOrder();
    }

    private class ListItem(int number) : IComparable<ListItem>
    {
        public int Number { get; } = number;

        public int CompareTo(ListItem? other) => DefaultComparer.Compare(this, other);

    }
}

public class SortFixture : IDisposable
{
    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

    private readonly RandomPersonGenerator _generator = new();

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

    [Test]
    public async Task Insert()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbefirst = new Person("__A", 99);
        _source.Add(shouldbefirst);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 100 people in the cache");

        await Assert.That(_results.Data.Items[0]).IsEqualTo(shouldbefirst);
    }

    [Test]
    public async Task Remove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var toRemove = people.ElementAt(20);
        people.RemoveAt(20);
        _source.RemoveAt(20);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");
        await Assert.That(_results.Messages[1].First().Item.Current).IsEqualTo(toRemove).Because("Incorrect item removed");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyOdds()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var odd = people.Select((p, idx) => new { p, idx }).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

        _source.RemoveMany(odd);

        await Assert.That(_results.Data.Count).IsEqualTo(50).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 10 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyReverseOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Replace()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbefirst = new Person("__A", 99);
        _source.ReplaceAt(10, shouldbefirst);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        await Assert.That(_results.Data.Items[0]).IsEqualTo(shouldbefirst);
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }
}
