#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

#if P_LINQ
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
using DynamicData.Reactive.PLinq;
#else
using DynamicData.Kernel;
using DynamicData.PLinq;
#endif
#endif

namespace DynamicData.Tests.Cache;

public sealed class SortingAndParallelBoundaryCoverageFixture
{
    private static readonly IComparer<Person> AgeAscendingComparer =
        SortExpressionComparer<Person>.Ascending(person => person.Age).ThenByAscending(person => person.Name);

    private static readonly IComparer<Person> AgeDescendingComparer =
        SortExpressionComparer<Person>.Descending(person => person.Age).ThenByAscending(person => person.Name);

    [Test]
    public async Task ObsoleteSortEmitsInitialLoadThenResetsWhenComparerChangesAboveThreshold()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var comparerChanged = new ReactiveUI.Primitives.Signals.Signal<IComparer<Person>>();
        using var results = source.Connect().Sort(comparerChanged, resetThreshold: 3).AsAggregator();

        var people = new[]
        {
            new Person("Charlie", 30),
            new Person("Alice", 10),
            new Person("Bob", 20),
        };

        source.AddOrUpdate(people);

        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Messages[0].SortedItems.SortReason).IsEqualTo(SortReason.InitialLoad);

        comparerChanged.OnNext(AgeAscendingComparer);

        await AssertNames(results.Messages.Last().SortedItems.Select(item => item.Value), "Alice", "Bob", "Charlie");

        comparerChanged.OnNext(AgeDescendingComparer);

        await AssertNames(results.Messages.Last().SortedItems.Select(item => item.Value), "Charlie", "Bob", "Alice");
        await Assert.That(results.Messages.Last().SortedItems.SortReason).IsEqualTo(SortReason.Reset);
    }

    [Test]
    public async Task ObsoleteSortResorterRepositionsMutatedItemWithoutWaiting()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var resort = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        using var results = source.Connect().Sort(AgeAscendingComparer, resort).AsAggregator();

        var people = new[]
        {
            new Person("Alice", 10),
            new Person("Bob", 20),
            new Person("Charlie", 30),
        };

        source.AddOrUpdate(people);
        people[2].Age = 5;
        resort.OnNext(Unit.Default);

        await AssertNames(results.Messages.Last().SortedItems.Select(item => item.Value), "Charlie", "Alice", "Bob");
        await Assert.That(results.Messages.Last().SortedItems.SortReason).IsEqualTo(SortReason.Reorder);
    }

    [Test]
    public async Task ObsoleteSortIgnoreEvaluatesUsesRefreshToMaintainOrderedSnapshot()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var results = source.Connect()
            .AutoRefresh(person => person.Age)
            .Sort(AgeAscendingComparer, SortOptimisations.IgnoreEvaluates)
            .AsAggregator();

        var alice = new Person("Alice", 10);
        var bob = new Person("Bob", 20);

        source.AddOrUpdate(new[] { alice, bob });
        bob.Age = 5;

        await AssertNames(results.Messages.Last().SortedItems.Select(item => item.Value), "Bob", "Alice");
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
    }

    [Test]
    public async Task SortAndVirtualizeKeepsOrderedBoundariesAcrossUpdateRefreshAndRangeShift()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var requests = new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(1, 3));
        var list = new List<Person>();
        using var results = source.Connect()
            .SortAndVirtualize(AgeAscendingComparer, requests)
            .Bind(list)
            .AsAggregator();

        var people = new[]
        {
            new Person("P1", 10),
            new Person("P2", 20),
            new Person("P3", 30),
            new Person("P4", 40),
            new Person("P5", 50),
        };

        source.AddOrUpdate(people);

        await AssertNames(list, "P2", "P3", "P4");

        source.AddOrUpdate(new Person("P3", 35));

        await AssertNames(list, "P2", "P3", "P4");
        await Assert.That(results.Messages.Last().Updates).IsEqualTo(1);

        source.AddOrUpdate(new Person("P0", 5));

        await AssertNames(list, "P1", "P2", "P3");
        await Assert.That(results.Messages.Last().Adds).IsEqualTo(1);
        await Assert.That(results.Messages.Last().Removes).IsEqualTo(1);

        requests.OnNext(new VirtualRequest(2, 2));

        await AssertNames(list, "P2", "P3");
    }

    [Test]
    public async Task SortAndPageMovesOverflowRequestToLastPageAndKeepsOrderedList()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var requests = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(1, 2));
        var list = new List<Person>();
        var paged = source.Connect().SortAndPage(AgeAscendingComparer, requests);
        using var contextResults = paged.AsAggregator();
        using var boundResults = paged.Bind(list).AsAggregator();

        var people = new[]
        {
            new Person("P1", 10),
            new Person("P2", 20),
            new Person("P3", 30),
            new Person("P4", 40),
            new Person("P5", 50),
        };

        source.AddOrUpdate(people);

        await AssertNames(list, "P1", "P2");

        requests.OnNext(new PageRequest(10, 2));

        await AssertNames(list, "P5");
        await Assert.That(boundResults.Messages.Last().Removes).IsEqualTo(2);
        await Assert.That(contextResults.Messages.Last().Context.Response.Page).IsEqualTo(3);
    }

    [Test]
    public async Task ObsoletePageVirtualiseAndTopPreserveSortedWindows()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var pageRequests = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(2, 2));
        using var virtualRequests = new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(1, 3));

        var sorted = source.Connect().Sort(AgeAscendingComparer, resetThreshold: 10);
        using var paged = sorted.Page(pageRequests).AsAggregator();
        using var virtualised = sorted.Virtualise(virtualRequests).AsAggregator();
        using var top = sorted.Top(2).AsAggregator();

        var people = new[]
        {
            new Person("P4", 40),
            new Person("P1", 10),
            new Person("P3", 30),
            new Person("P2", 20),
        };

        source.AddOrUpdate(people);

        await AssertNames(paged.Messages.Last().SortedItems.Select(item => item.Value), "P3", "P4");
        await AssertNames(virtualised.Messages.Last().SortedItems.Select(item => item.Value), "P2", "P3", "P4");
        await AssertNames(top.Messages.Last().SortedItems.Select(item => item.Value), "P1", "P2");
    }

    [Test]
    public async Task TopRejectsNonPositiveSize()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        var sorted = source.Connect().Sort(AgeAscendingComparer, resetThreshold: 10);

        await Assert.That(() => source.Connect().Top(AgeAscendingComparer, 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => sorted.Top(0)).Throws<ArgumentOutOfRangeException>();
    }

#if P_LINQ
    [Test]
    public async Task ParallelFilterCoversSequentialAndParallelThresholdBranchesWithOrderedResults()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        using var results = source.Connect()
            .Filter(person => person.Age >= 30, new ParallelisationOptions(ParallelType.Ordered, threshold: 3))
            .AsAggregator();

        source.AddOrUpdate(new[]
        {
            new Person("P1", 10),
            new Person("P2", 40),
        });

        await AssertNames(results.Data.Items.OrderBy(person => person.Age), "P2");

        source.AddOrUpdate(new[]
        {
            new Person("P3", 30),
            new Person("P4", 50),
            new Person("P5", 20),
        });

        await AssertNames(results.Data.Items.OrderBy(person => person.Age), "P3", "P2", "P4");
    }

    [Test]
    public async Task ParallelTransformSafeReportsErrorsAndRetainsSuccessfulUpdates()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        var errors = new List<Error<Person, string>>();
        using var results = source.Connect()
            .TransformSafe(
                static person =>
                {
                    if (person.Age == 20)
                    {
                        throw new InvalidOperationException("Age 20 is intentionally rejected.");
                    }

                    return $"{person.Name}:{person.Age}";
                },
                errors.Add,
                new ParallelisationOptions(ParallelType.Parallelise, threshold: 2))
            .AsAggregator();

        source.AddOrUpdate(new[]
        {
            new Person("P1", 10),
            new Person("P2", 20),
            new Person("P3", 30),
        });

        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Key).IsEqualTo("P2");
        await Assert.That(results.Data.Items.OrderBy(value => value).ToArray())
            .IsEquivalentTo(new[] { "P1:10", "P3:30" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ParallelSubscribeManyDisposesUpdatedRemovedAndClearedSubscriptions()
    {
        using var source = new SourceCache<Person, string>(person => person.Name);
        var disposed = new List<string>();
        using var results = source.Connect()
            .SubscribeMany(
                (person, key) => Disposable.Create(() => disposed.Add($"{key}:{person.Age}")),
                new ParallelisationOptions(ParallelType.Ordered, threshold: 2))
            .AsAggregator();

        source.AddOrUpdate(new[]
        {
            new Person("P1", 10),
            new Person("P2", 20),
        });

        source.AddOrUpdate(new Person("P1", 15));
        source.Remove("P2");
        source.Clear();

        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(disposed.OrderBy(value => value).ToArray())
            .IsEquivalentTo(new[] { "P1:10", "P1:15", "P2:20" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
#endif

    private static async Task AssertNames(IEnumerable<Person> actual, params string[] expected)
    {
        var actualNames = actual.Select(person => person.Name).ToArray();
        await Assert.That(actualNames).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
