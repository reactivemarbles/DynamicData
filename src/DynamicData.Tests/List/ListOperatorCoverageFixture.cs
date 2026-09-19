#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class ListOperatorCoverageFixture
{
    [Test]
    public async Task DynamicFilterRequeriesExistingItemsWhenPredicateChanges()
    {
        using var source = new SourceList<int>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<int, bool>>(static value => value % 2 == 0);
        using var results = source.Connect().Filter(predicates, ListFilterPolicy.CalculateDiff).AsAggregator();

        source.AddRange(new[] { 1, 2, 3, 4 });
        predicates.OnNext(static value => value >= 3);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 3, 4 });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task DynamicFilterClearAndReplacePublishesReplacementSnapshot()
    {
        using var source = new SourceList<int>();
        using var predicates = new ReactiveUI.Primitives.Signals.StateSignal<Func<int, bool>>(static value => value < 4);
        using var results = source.Connect().Filter(predicates, ListFilterPolicy.ClearAndReplace).AsAggregator();

        source.AddRange(new[] { 1, 2, 3, 4, 5 });
        predicates.OnNext(static value => value > 3);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 4, 5 });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(3);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(2);
    }

    [Test]
    public async Task DistinctValuesTracksRefreshesAndDuplicateRemoval()
    {
        using var source = new TestSourceList<MutableListItem>();
        using var results = source.Connect().DistinctValues(static item => item.Group).AsAggregator();

        var first = new MutableListItem("first", "A");
        var second = new MutableListItem("second", "A");
        source.AddRange(new[] { first, second });

        first.Group = "B";
        source.Refresh(0);
        source.Remove(second);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { "B" });
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(0);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task GroupOnRegrouperMovesItemsBetweenGroups()
    {
        using var source = new SourceList<MutableListItem>();
        using var regrouper = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        using var results = source.Connect().GroupOn(static item => item.Group, regrouper).AsAggregator();

        var item = new MutableListItem("item", "A");
        source.Add(item);

        item.Group = "B";
        regrouper.OnNext(Unit.Default);

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].GroupKey).IsEqualTo("B");
        await Assert.That(results.Data.Items[0].List.Items).IsEquivalentTo(new[] { item });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task GroupOnRefreshMovesMutableItemBetweenGroups()
    {
        using var source = new SourceList<MutableListItem>();
        using var results = source.Connect().AutoRefresh(item => item.Group).GroupOn(static item => item.Group).AsAggregator();

        var item = new MutableListItem("item", "A");
        source.Add(item);

        item.Group = "B";

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].GroupKey).IsEqualTo("B");
        await Assert.That(results.Data.Items[0].List.Items).IsEquivalentTo(new[] { item });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GroupWithImmutableStateRegrouperPublishesNewSnapshots()
    {
        using var source = new SourceList<MutableListItem>();
        using var regrouper = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        using var results = source.Connect().GroupWithImmutableState(static item => item.Group, regrouper).AsAggregator();

        var item = new MutableListItem("item", "A");
        source.Add(item);

        item.Group = "B";
        regrouper.OnNext(Unit.Default);

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].Key).IsEqualTo("B");
        await Assert.That(results.Data.Items[0].Items).IsEquivalentTo(new[] { item });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task GroupOnPropertyMovesWhenObservedPropertyChanges()
    {
        using var source = new SourceList<Person>();
        using var results = source.Connect().GroupOnProperty(person => person.Age).AsAggregator();

        var person = new Person("Adult", 40);
        source.Add(person);

        person.Age = 41;

        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].GroupKey).IsEqualTo(41);
        await Assert.That(results.Data.Items[0].List.Items).IsEquivalentTo(new[] { person });
        await Assert.That(results.Messages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TransformAsyncUsesIndexesPreviousValuesAndRefreshPolicy()
    {
        using var source = new TestSourceList<int>();
        var calls = new List<string>();
        using var results = source.Connect()
            .TransformAsync<int, string>(
                (value, previous, index, _) =>
                {
                    calls.Add($"{value}:{index}:{(previous.HasValue ? previous.Value : "none")}");
                    return Task.FromResult($"{value}:{index}");
                },
                transformOnRefresh: false)
            .AsAggregator();

        source.AddRange(new[] { 10, 20 });
        source.Insert(1, 15);
        source.ReplaceAt(1, 16);
        source.Move(2, 0);
        source.Refresh(0);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { "20:1", "10:0", "16:1" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(calls).IsEquivalentTo(new[] { "10:0:none", "20:1:none", "15:1:none", "16:1:15:1" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
    }

    [Test]
    public async Task CombineRequiresAtLeastOneOtherSource()
    {
        using var source = new SourceList<int>();

        await Assert.That(() => source.Connect().Or()).Throws<ArgumentException>();
    }

    [Test]
    public async Task DynamicObservableListCombineTracksAddedAndRemovedSources()
    {
        using var first = new SourceList<int>();
        using var second = new SourceList<int>();
        using var sources = new SourceList<IObservableList<int>>();
        using var results = sources.Or().AsAggregator();

        sources.Add(first);
        await Assert.That(results.Messages).IsEmpty();

        first.Add(1);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1 });

        sources.Add(second);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        second.Add(2);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1, 2 });

        sources.Remove(first);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 2 });
    }

    private sealed class MutableListItem(string name, string group) : AbstractNotifyPropertyChanged
    {
        private string _group = group;

        public string Group
        {
            get => _group;
            set => SetAndRaise(ref _group, value);
        }

        public string Name { get; } = name;

        public override string ToString() => $"{Name}:{Group}";
    }
}
