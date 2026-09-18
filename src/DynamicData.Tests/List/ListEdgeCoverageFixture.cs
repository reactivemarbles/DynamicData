namespace DynamicData.Tests.List;

public class ListEdgeCoverageFixture
{
    [Test]
    public async Task DynamicXorRechecksMembershipWhenSourceIsAddedAndRemoved()
    {
        using var first = new SourceList<int>();
        using var second = new SourceList<int>();
        using var sources = new SourceList<IObservableList<int>>();
        using var results = sources.Xor().AsAggregator();

        sources.Add(first);
        first.Add(1);
        sources.Add(second);
        second.Add(1);
        sources.Remove(second);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[2].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1 });
    }

    [Test]
    public async Task DynamicExceptRechecksFirstSourceItemsWhenOtherSourceIsAddedAndRemoved()
    {
        using var first = new SourceList<int>();
        using var second = new SourceList<int>();
        using var sources = new SourceList<IObservableList<int>>();
        using var results = sources.Except().AsAggregator();

        first.Add(1);
        second.Add(1);

        sources.Add(first);
        sources.Add(second);
        sources.Remove(second);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[2].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1 });
    }

    [Test]
    public async Task DynamicCombinerIgnoresFailedChildrenAndKeepsHealthyChildrenActive()
    {
        using var failedChild = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var healthyChild = new SourceList<int>();
        using var sources = new SourceList<IObservable<IChangeSet<int>>>();
        using var results = sources.Or().AsAggregator();

        sources.Add(failedChild);
        sources.Add(healthyChild.Connect());
        failedChild.OnError(new InvalidOperationException("child failed"));
        healthyChild.Add(42);

        await Assert.That(results.Exception).IsNull();
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 42 });
        await Assert.That(results.Messages).HasSingleItem();
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
    }

    [Test]
    public async Task TransformAsyncTransformOnRefreshUsesPreviousValue()
    {
        using var source = new TestSourceList<int>();
        var calls = new List<string>();
        using var results = source.Connect()
            .TransformAsync<int, string>(
                (value, previous, index, _) =>
                {
                    calls.Add(previous.HasValue ? previous.Value : "none");
                    return Task.FromResult($"{value}:{index}");
                },
                transformOnRefresh: true)
            .AsAggregator();

        source.Add(1);
        source.Refresh(0);

        await Assert.That(calls).IsEquivalentTo(new[] { "none", "1:0" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Replaced).IsEqualTo(1);
    }

    [Test]
    public async Task TransformAsyncClearAndRemoveRangeUpdateTransformedState()
    {
        using var source = new SourceList<int>();
        using var results = source.Connect()
            .TransformAsync<int, string>((value, _, index, _) => Task.FromResult($"{value}:{index}"))
            .AsAggregator();

        source.AddRange(new[] { 1, 2, 3, 4 });
        source.RemoveRange(1, 2);
        source.Clear();

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(2);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(2);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task DynamicFilterRefreshPromotesRefreshesAndDemotesItems()
    {
        using var source = new TestSourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        var item = new EdgeItem("one", "A", included: false);
        source.Add(item);

        item.Included = true;
        source.Refresh(0);
        source.Refresh(0);
        item.Included = false;
        source.Refresh(0);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task DynamicFilterPredicateReplacementAfterRefreshUsesLatestMatchState()
    {
        using var source = new TestSourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        var item = new EdgeItem("one", "A", included: true);
        source.Add(item);
        source.Refresh(0);
        predicates.OnNext(static _ => false);
        predicates.OnNext(static item => item.Included);

        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[3].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item });
    }

    [Test]
    public async Task DynamicFilterMixedRefreshBatchForwardsAndDemotes()
    {
        using var source = new TestSourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        var retained = new EdgeItem("retained", "A", included: true);
        var demoted = new EdgeItem("demoted", "A", included: true);
        source.AddRange(new[] { retained, demoted });

        demoted.Included = false;
        source.Refresh(new[] { 0, 1 });

        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(2);
        await Assert.That(results.Messages[1].Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { retained });
    }

    [Test]
    public async Task DynamicFilterReplaceHandlesMatchTransitions()
    {
        using var source = new SourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        var excluded = new EdgeItem("excluded", "A", included: false);
        var included = new EdgeItem("included", "A", included: true);
        var stillIncluded = new EdgeItem("still included", "A", included: true);
        var excludedAgain = new EdgeItem("excluded again", "A", included: false);

        source.Add(excluded);
        source.ReplaceAt(0, included);
        source.ReplaceAt(0, stillIncluded);
        source.ReplaceAt(0, excludedAgain);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task DynamicFilterRefreshMatchStateDrivesFollowingReplace()
    {
        using var source = new TestSourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        var promoted = new EdgeItem("promoted", "A", included: false);
        var replacementAfterPromotion = new EdgeItem("replacement after promotion", "A", included: true);
        var replacementAfterDemotion = new EdgeItem("replacement after demotion", "A", included: true);

        source.Add(promoted);
        promoted.Included = true;
        source.Refresh(0);
        source.ReplaceAt(0, replacementAfterPromotion);
        replacementAfterPromotion.Included = false;
        source.Refresh(0);
        source.ReplaceAt(0, replacementAfterDemotion);

        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[3].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { replacementAfterDemotion });
    }

    [Test]
    public async Task DynamicFilterRefreshThenReplaceInSameBatchUsesUpdatedMatchState()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<EdgeItem>>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Filter(predicates).AsAggregator();

        var item = new EdgeItem("item", "A", included: false);
        var replacement = new EdgeItem("replacement", "A", included: true);

        source.OnNext(new ChangeSet<EdgeItem> { new(ListChangeReason.Add, item, 0) });

        item.Included = true;
        source.OnNext(new ChangeSet<EdgeItem>
        {
            new(ListChangeReason.Refresh, item, 0),
            new(ListChangeReason.Replace, replacement, ReactiveUI.Primitives.Optional<EdgeItem>.Create(item), 0, 0)
        });

        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[0].Replaced).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { replacement });
    }

    [Test]
    public async Task DynamicFilterExplicitEqualOrSameReferenceReplaceStaysReplace()
    {
        using var referenceSource = new SourceList<EdgeItem>();
        using var referencePredicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var referenceResults = referenceSource.Connect().Filter(referencePredicates).AsAggregator();

        var item = new EdgeItem("same", "A", included: true);
        referenceSource.Add(item);
        referenceSource.ReplaceAt(0, item);

        await Assert.That(referenceResults.Messages.Count).IsEqualTo(2);
        await Assert.That(referenceResults.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(referenceResults.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(referenceResults.Messages[1].Refreshes).IsEqualTo(0);

        using var valueSource = new SourceList<int>();
        using var valuePredicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<int, bool>>(static _ => true);
        using var valueResults = valueSource.Connect().Filter(valuePredicates).AsAggregator();

        valueSource.Add(1);
        valueSource.ReplaceAt(0, 1);

        await Assert.That(valueResults.Messages.Count).IsEqualTo(2);
        await Assert.That(valueResults.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(valueResults.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(valueResults.Messages[1].Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task DynamicFilterRemoveRangeRemovesOnlyMatchedItems()
    {
        using var source = new SourceList<EdgeItem>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<EdgeItem, bool>>(static item => item.Included);
        using var results = source.Connect().Filter(predicates).AsAggregator();

        source.AddRange(new[]
        {
            new EdgeItem("one", "A", included: true),
            new EdgeItem("two", "A", included: false),
            new EdgeItem("three", "A", included: true)
        });

        source.RemoveRange(0, 3);

        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(2);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(2);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task GroupOperatorsRemoveEmptyGroupsOnRemoveAndClear()
    {
        using var source = new SourceList<EdgeItem>();
        using var liveGroups = source.Connect().GroupOn(static item => item.Group).AsAggregator();
        using var immutableGroups = source.Connect().GroupWithImmutableState(static item => item.Group).AsAggregator();

        var first = new EdgeItem("one", "A", included: true);
        var second = new EdgeItem("two", "A", included: true);
        source.AddRange(new[] { first, second });
        source.Remove(first);
        source.Clear();

        await Assert.That(liveGroups.Data.Items).IsEmpty();
        await Assert.That(immutableGroups.Data.Items).IsEmpty();
        await Assert.That(liveGroups.Messages.Last().Removes).IsEqualTo(1);
        await Assert.That(immutableGroups.Messages.Last().Removes).IsEqualTo(1);
    }

    private sealed class EdgeItem(string name, string group, bool included)
    {
        public string Group { get; set; } = group;

        public bool Included { get; set; } = included;

        public string Name { get; } = name;

        public override string ToString() => $"{Name}:{Group}:{Included}";
    }
}
