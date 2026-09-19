#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class PageFixture : IDisposable
{
    private readonly PagedChangeSetAggregator<Person, string> _aggregators;

    private readonly IComparer<Person> _comparer;

    private readonly RandomPersonGenerator _generator = new();

    private readonly ReactiveUI.Primitives.Signals.ISignal<IPageRequest> _pager;

    private readonly ReactiveUI.Primitives.Signals.ISignal<IComparer<Person>> _sort;

    private readonly ISourceCache<Person, string> _source;

    public PageFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
        _sort = new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>>(_comparer);
        _pager = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(1, 25));

        _aggregators = _source.Connect().Sort(_sort, resetThreshold: 200).Page(_pager).AsAggregator();
    }

    [Test]
    public async Task ChangePage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(2, 25));

        var expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task ChangePageSize()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(1, 50));

        await Assert.That(_aggregators.Messages[1].Response.Page).IsEqualTo(1).Because("Should be page 1");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(50).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    public void Dispose()
    {
        _source.Dispose();
        _aggregators.Dispose();
        _pager.Dispose();
        _sort.Dispose();
    }

    [Test]
    public async Task PageGreaterThanNumberOfPagesAvailable()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(10, 25));

        await Assert.That(_aggregators.Messages[1].Response.Page).IsEqualTo(4).Because("Page should move to the last page");

        var expectedResult = people.OrderBy(p => p, _comparer).Skip(75).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task PageInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_aggregators.Data.Count).IsEqualTo(25).Because("Should be 25 people in the cache");
        await Assert.That(_aggregators.Messages[0].Response.PageSize).IsEqualTo(25).Because("Page size should be 25");
        await Assert.That(_aggregators.Messages[0].Response.Page).IsEqualTo(1).Because("Should be page 1");
        await Assert.That(_aggregators.Messages[0].Response.Pages).IsEqualTo(4).Because("Should be page 4 pages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[0].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task ReorderBelowThreshold()
    {
        var people = _generator.Take(50).ToArray();
        _source.AddOrUpdate(people);

        var changed = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);
        _sort.OnNext(changed);

        var expectedResult = people.OrderBy(p => p, changed).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages.Last().SortedItems.ToList();
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task ThrowsForNegativePage() => await Assert.That(() => _pager.OnNext(new PageRequest(-1, 1))).Throws<ArgumentException>();

    [Test]
    public async Task ThrowsForNegativeSizeParameters() => await Assert.That(() => _pager.OnNext(new PageRequest(1, -1))).Throws<ArgumentException>();
}
