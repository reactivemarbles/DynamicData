#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class SortObservableFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _cache;

    private readonly SortExpressionComparer<Person> _comparer;

    private readonly ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>> _comparerObservable;

    private readonly RandomPersonGenerator _generator = new();

    private readonly SortedChangeSetAggregator<Person, string> _results;

    //  private IComparer<Person> _comparer;

    public SortObservableFixture()
    {
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
        _comparerObservable = new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>>(_comparer);
        _cache = new SourceCache<Person, string>(p => p.Name);
        //  _sortController = new SortController<Person>(_comparer);

        _results = new SortedChangeSetAggregator<Person, string>(_cache.Connect().Sort(_comparerObservable, resetThreshold: 25));
    }

    [Test]
    public async Task ChangeSort()
    {
        var people = _generator.Take(100).ToArray();
        _cache.AddOrUpdate(people);

        var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

        _comparerObservable.OnNext(desc);
        var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _results.Messages[0].SortedItems.ToList();
        var movesCount = _results.Messages[0].Moves;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task ChangeSortAboveThreshold()
    {
        var people = _generator.Take(30).ToArray();
        _cache.AddOrUpdate(people);

        var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

        _comparerObservable.OnNext(desc);
        var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var items = _results.Messages.Last().SortedItems;
        var actualResult = items.ToList();
        var sortReason = items.SortReason;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
        await Assert.That(sortReason).IsEqualTo(SortReason.Reset);
    }

    [Test]
    public async Task ChangeSortWithinThreshold()
    {
        var people = _generator.Take(20).ToArray();
        _cache.AddOrUpdate(people);

        var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

        _comparerObservable.OnNext(desc);
        var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var items = _results.Messages.Last().SortedItems;
        var actualResult = items.ToList();
        var sortReason = items.SortReason;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
        await Assert.That(sortReason).IsEqualTo(SortReason.Reorder);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _results.Dispose();
        _comparerObservable.OnCompleted();
        _comparerObservable.Dispose();
    }

    [Test]
    public async Task InlineChanges()
    {
        var people = _generator.Take(10000).ToArray();
        _cache.AddOrUpdate(people);

        //apply mutable changes to the items
        var random = new Random();
        var toChange = people.OrderBy(x => Guid.NewGuid()).Take(10).ToList();

        toChange.ForEach(p => p.Age = random.Next(0, 100));

        _cache.Refresh(toChange);

        var expected = people.OrderBy(t => t, _comparer).ToList();
        var actual = _results.Messages.Last().SortedItems.Select(kv => kv.Value).ToList();
        await Assert.That(actual).IsEquivalentTo(expected);

        var list = new ObservableCollectionExtended<Person>();
        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
        foreach (var message in _results.Messages)
        {
            adaptor.Adapt(message, list);
        }

        await Assert.That(list).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Reset()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).OrderBy(x => Guid.NewGuid()).ToArray();
        _cache.AddOrUpdate(people);
        _comparerObservable.OnNext(SortExpressionComparer<Person>.Descending(p => p.Age));
        _comparerObservable.OnNext(_comparer);

        var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _results.Messages[2].SortedItems.ToList();
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _cache.AddOrUpdate(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _results.Messages[0].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }
}
