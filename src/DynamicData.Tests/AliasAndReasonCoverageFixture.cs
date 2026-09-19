#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
using CacheAliases = DynamicData.Reactive.Alias.ObservableCacheAlias;
using ListAliases = DynamicData.Reactive.Alias.ObservableListAlias;
#else
using DynamicData.Kernel;
using CacheAliases = DynamicData.Alias.ObservableCacheAlias;
using ListAliases = DynamicData.Alias.ObservableListAlias;
#endif

namespace DynamicData.Tests;

public sealed class AliasAndReasonCoverageFixture
{
    [Test]
    public async Task ListWhereAliasesTrackStaticAndChangingPredicates()
    {
        using var source = new SourceList<int>();
        using var predicates = new ReactiveUI.Primitives.Signals.Signal<Func<int, bool>>();
        using var staticResults = ListAliases.Where(source.Connect(), static item => item % 2 == 0).AsAggregator();
        using var dynamicResults = ListAliases.Where(source.Connect(), predicates).AsAggregator();
        source.AddRange(new[] { 1, 2, 3, 4 });
        await Assert.That(dynamicResults.Data.Items).IsEmpty();
        predicates.OnNext(static item => item > 2);
        await Assert.That(dynamicResults.Data.Items).IsEquivalentTo(new[] { 3, 4 });
        source.Remove(4);
        predicates.OnNext(static item => item < 3);

        await Assert.That(staticResults.Data.Items).IsEquivalentTo(new[] { 2 });
        await Assert.That(dynamicResults.Data.Items).IsEquivalentTo(new[] { 1, 2 });
    }

    [Test]
    public async Task ListSelectManyAliasRemovesChildrenWhenParentIsRemoved()
    {
        using var source = new SourceList<int[]>();
        using var results = ListAliases.SelectMany<int, int[]>(source.Connect(), static children => children).AsAggregator();
        var first = new[] { 1, 2 };
        var second = new[] { 3, 4 };
        source.AddRange(new[] { first, second });
        source.Remove(first);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 3, 4 });
        await Assert.That(results.Messages.Last().Removes).IsEqualTo(2);
        source.Clear();
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task CacheSelectSafeAliasSupportsForcedRetransformAndItemErrors()
    {
        using var source = new SourceCache<KeyedItem, int>(static item => item.Key);
        using var force = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        var errors = new List<Error<KeyedItem, int>>();
        var multiplier = 2;
        using var results = CacheAliases.SelectSafe<int, KeyedItem, int>(
            source.Connect(),
            item => item.Value < 0 ? throw new InvalidOperationException("invalid value") : item.Value * multiplier,
            errors.Add,
            force).AsAggregator();
        source.AddOrUpdate(new KeyedItem(1, 10));
        await Assert.That(results.Data.Lookup(1).Value).IsEqualTo(20);

        multiplier = 3;
        force.OnNext(Unit.Default);
        source.AddOrUpdate(new KeyedItem(2, -1));

        await Assert.That(results.Data.Lookup(1).Value).IsEqualTo(30);
        await Assert.That(results.Data.Lookup(2).HasValue).IsFalse();
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Key).IsEqualTo(2);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task CacheReasonFilterSuppressesEmptyMessagesAndPreservesSelectedChanges()
    {
        using var source = new SourceCache<KeyedItem, int>(static item => item.Key);
        var messages = new List<IChangeSet<KeyedItem, int>>();
        using var subscription = source.Connect().WhereReasonsAre(ChangeReason.Add, ChangeReason.Remove).Subscribe(messages.Add);
        source.AddOrUpdate(new KeyedItem(1, 10));
        source.AddOrUpdate(new KeyedItem(1, 20));
        source.Refresh(source.Lookup(1).Value);
        source.RemoveKey(1);

        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[0].Adds).IsEqualTo(1);
        await Assert.That(messages[1].Removes).IsEqualTo(1);
        await Assert.That(messages[1].Single().Current.Value).IsEqualTo(20);
        await Assert.That(() => source.Connect().WhereReasonsAre()).Throws<ArgumentException>();
    }

    [Test]
    public async Task ExcludingListMutationsStripsIndicesWhileRefreshOnlyExclusionRetainsThem()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        var stripped = new List<IChangeSet<int>>();
        var preserved = new List<IChangeSet<int>>();
        using var first = source.WhereReasonsAreNot(ListChangeReason.Remove, ListChangeReason.Add).Subscribe(stripped.Add);
        using var second = source.WhereReasonsAreNot(ListChangeReason.Refresh).Subscribe(preserved.Add);
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.Add, 10, 2),
            new(ListChangeReason.Replace, 20, ReactiveUI.Primitives.Optional<int>.Create(10), 2, 2),
            new(ListChangeReason.Refresh, 20, 2)
        });

        await Assert.That(stripped.Count).IsEqualTo(1);
        await Assert.That(stripped[0].Count).IsEqualTo(2);
        await Assert.That(stripped[0].First().Item.CurrentIndex).IsEqualTo(-1);
        await Assert.That(stripped[0].First().Item.PreviousIndex).IsEqualTo(-1);
        await Assert.That(stripped[0].Last().Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(stripped[0].Last().Item.CurrentIndex).IsEqualTo(2);
        await Assert.That(stripped[0].Last().Item.Current).IsEqualTo(20);
        await Assert.That(preserved.Count).IsEqualTo(1);
        await Assert.That(preserved[0].Count).IsEqualTo(2);
        await Assert.That(preserved[0].First().Item.CurrentIndex).IsEqualTo(2);
        await Assert.That(preserved[0].Last().Item.PreviousIndex).IsEqualTo(2);
        await Assert.That(() => source.WhereReasonsAreNot()).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RemovingIndicesRetainsRefreshSourcePositionAndOmitsMoves(bool filterReasons)
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        var messages = new List<IChangeSet<int>>();
        var output = filterReasons
            ? source.WhereReasonsAre(ListChangeReason.Refresh, ListChangeReason.Moved)
            : source.RemoveIndex();
        using var subscription = output.Subscribe(messages.Add);
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.Refresh, 20, 2),
            new(20, 1, 2)
        });

        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].Count).IsEqualTo(1);
        var refresh = messages[0].Single();
        await Assert.That(refresh.Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(refresh.Item.Current).IsEqualTo(20);
        await Assert.That(refresh.Item.CurrentIndex).IsEqualTo(2);
    }

    [Test]
    public async Task StartWithKeyedItemEmitsInitialAddThenSourceUpdate()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<KeyedItem, int>>();
        var initial = new KeyedItem(7, 10);
        var replacement = new KeyedItem(7, 20);
        using var results = source.StartWithItem(initial).AsAggregator();
        await Assert.That(results.Data.Lookup(7).Value).IsSameReferenceAs(initial);

        source.OnNext(new ChangeSet<KeyedItem, int>
        {
            new(ChangeReason.Update, 7, replacement, ReactiveUI.Primitives.Optional<KeyedItem>.Create(initial))
        });

        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(1);
        await Assert.That(results.Data.Lookup(7).Value).IsSameReferenceAs(replacement);
    }

    private sealed record KeyedItem(int Key, int Value) : IKey<int>;
}
