#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

// Bind to a readonly observable collection
[InheritsTests]
public sealed class SortAndBindObservableToReadOnlyObservableCollection : SortAndBindObservableFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var aggregator = Cache.Connect().SortAndBind(out var list, ComparerObservable).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a list
[InheritsTests]
public sealed class SortAndBindObservableToList : SortAndBindObservableFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new List<Person>(100);
        var aggregator = Cache.Connect().SortAndBind(list, ComparerObservable).AsAggregator();

        return (aggregator, list);
    }
}

public abstract class SortAndBindObservableFixture : IDisposable
{
    protected readonly ISourceCache<Person, string> Cache = new SourceCache<Person, string>(p => p.Name);

    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly IList<Person> _boundList;
    private readonly SortExpressionComparer<Person> _oldestComparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);
    private readonly SortExpressionComparer<Person> _defaultComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

    private protected readonly ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>> ComparerObservable;

    protected SortAndBindObservableFixture()
    {
        ComparerObservable = new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>>(_defaultComparer);

        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        var args = SetUpTests();
#pragma warning restore CA2214

        // bind and sort in one hit

        _results = args.Aggregrator;
        _boundList = args.List;
    }

    protected abstract (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests();

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        Cache.AddOrUpdate(people);

        var defaultOrder = people.OrderBy(p => p, _defaultComparer).ToList();
        await Assert.That(_boundList.SequenceEqual(defaultOrder)).IsTrue();
    }

    [Test]
    public async Task ChangeSort()
    {
        var people = _generator.Take(100).ToArray();
        Cache.AddOrUpdate(people);

        // change to oldest first sort
        ComparerObservable.OnNext(_oldestComparer);

        var oldestFirst = people.OrderBy(p => p, _oldestComparer).ToList();
        await Assert.That(_boundList.SequenceEqual(oldestFirst)).IsTrue();

        // and back again
        ComparerObservable.OnNext(_defaultComparer);

        var defaultOrder = people.OrderBy(p => p, _defaultComparer).ToList();
        await Assert.That(_boundList.SequenceEqual(defaultOrder)).IsTrue();
    }

    public void Dispose()
    {
        Cache.Dispose();
        _results.Dispose();
        ComparerObservable.OnCompleted();
        ComparerObservable.Dispose();
    }

}
