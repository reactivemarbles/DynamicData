namespace DynamicData.Tests.List;

public class ListTransformBoundaryCoverageFixture
{
    [Test]
    public async Task TransformHandlesUnindexedRemoveRangeClearAndReplace()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<BoundaryItem>>();
        var calls = new List<string>();
        using var results = source
            .Transform<BoundaryItem, string>((item, previous, index) =>
            {
                calls.Add($"{item.Name}:{index}:{(previous.HasValue ? previous.Value : "none")}");
                return $"{item.Name}:{index}";
            })
            .AsAggregator();

        var first = new BoundaryItem("first");
        var second = new BoundaryItem("second");
        var third = new BoundaryItem("third");
        var replacement = new BoundaryItem("replacement");

        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.AddRange, new[] { first, second, third }, 0)
        });
        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.RemoveRange, new[] { first, third })
        });
        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.Replace, replacement, ReactiveUI.Primitives.Optional<BoundaryItem>.Create(second), -1, -1)
        });
        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.Clear, new[] { replacement })
        });

        await Assert.That(results.Data.Items).IsEmpty();
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(2);
        await Assert.That(results.Messages[2].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[3].Removes).IsEqualTo(1);
        await Assert.That(calls).IsEquivalentTo(
            new[] { "first:0:none", "second:1:none", "third:2:none", "replacement:0:second:1" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TransformThrowsWhenMoveDoesNotSpecifyCurrentIndex()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var results = source.Transform(static value => value.ToString()).AsAggregator();

        source.OnNext(new ChangeSet<int> { new(ListChangeReason.AddRange, new[] { 1, 2 }, 0) });
#if REACTIVE_TESTS
        source.OnNext(new ChangeSet<int> { new(ListChangeReason.Moved, 1, ReactiveUI.Primitives.Optional<int>.None, -1, 0) });
        await Assert.That(results.Exception).IsTypeOf<UnspecifiedIndexException>();
#else
        await Assert.That(() => source.OnNext(new ChangeSet<int> { new(ListChangeReason.Moved, 1, ReactiveUI.Primitives.Optional<int>.None, -1, 0) })).Throws<UnspecifiedIndexException>();
        await Assert.That(results.Exception).IsNull();
#endif
    }

    [Test]
    public async Task TransformAsyncPropagatesFactoryErrorsAndStopsProcessingBatch()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        var error = new InvalidOperationException("transform failed");
        using var results = source
            .TransformAsync<int, string>((value, _, _, _) =>
                value == 2 ? Task.FromException<string>(error) : Task.FromResult(value.ToString()))
            .AsAggregator();

        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.AddRange, new[] { 1, 2, 3 }, 0)
        });

        await Assert.That(results.Exception).IsSameReferenceAs(error);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task TransformAsyncMoveReplaceRefreshAndClearMaintainIndexes()
    {
        using var source = new TestSourceList<int>();
        var calls = new List<string>();
        using var results = source.Connect()
            .TransformAsync<int, string>((value, previous, index, _) =>
            {
                calls.Add($"{value}:{index}:{(previous.HasValue ? previous.Value : "none")}");
                return Task.FromResult($"{value}:{index}");
            }, transformOnRefresh: true)
            .AsAggregator();

        source.AddRange(new[] { 10, 20, 30 });
        source.Move(0, 2);
        source.ReplaceAt(1, 25);
        source.Refresh(1);
        source.Clear();

        await Assert.That(results.Data.Items).IsEmpty();
        await Assert.That(results.Messages.Count).IsEqualTo(5);
        await Assert.That(results.Messages[1].Moves).IsEqualTo(1);
        await Assert.That(results.Messages[2].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[3].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[4].Removes).IsEqualTo(3);
        await Assert.That(calls).IsEquivalentTo(
            new[] { "10:0:none", "20:1:none", "30:2:none", "25:1:30:2", "25:1:25:1" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TransformAsyncUnindexedRemoveRemovesMatchingReferenceSourceItem()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<BoundaryItem>>();
        using var results = source
            .TransformAsync<BoundaryItem, string>(static item => Task.FromResult(item.Name))
            .AsAggregator();

        var first = new BoundaryItem("first");
        var second = new BoundaryItem("second");
        var third = new BoundaryItem("third");

        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.AddRange, new[] { first, second, third }, 0)
        });
        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.Remove, second)
        });

        await Assert.That(results.Exception).IsNull();
        await Assert.That(results.Data.Items).IsEquivalentTo(
            new[] { "first", "third" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TransformAsyncUnindexedRemoveRangeRemovesMatchingReferenceSourceItems()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<BoundaryItem>>();
        using var results = source
            .TransformAsync<BoundaryItem, string>(static item => Task.FromResult(item.Name))
            .AsAggregator();

        var first = new BoundaryItem("first");
        var second = new BoundaryItem("second");
        var third = new BoundaryItem("third");
        var fourth = new BoundaryItem("fourth");

        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.AddRange, new[] { first, second, third, fourth }, 0)
        });
        source.OnNext(new ChangeSet<BoundaryItem>
        {
            new(ListChangeReason.RemoveRange, new[] { first, third })
        });

        await Assert.That(results.Exception).IsNull();
        await Assert.That(results.Data.Items).IsEquivalentTo(
            new[] { "second", "fourth" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task StaticFilterProcessesRangeReplaceRemoveAndClear()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var results = source.Filter(static value => value % 2 == 0).AsAggregator();

        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.AddRange, new[] { 1, 2, 3, 4, 8 }, 0)
        });
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.Replace, 6, ReactiveUI.Primitives.Optional<int>.Create(2), 1, 1),
            new(ListChangeReason.Replace, 7, ReactiveUI.Primitives.Optional<int>.Create(4), 3, 3)
        });
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.RemoveRange, new[] { 6, 3, 7 }, 1)
        });
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.Clear, new[] { 1, 8 })
        });

        await Assert.That(results.Data.Items).IsEmpty();
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(3);
        await Assert.That(results.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[3].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task SourceListConnectPredicateUsesIndexedStaticFilterPaths()
    {
        using var source = new SourceList<int>();
        using var results = source.Connect(static value => value % 2 == 0).AsAggregator();

        source.AddRange(new[] { 1, 2, 4 });
        source.ReplaceAt(1, 6);
        source.ReplaceAt(1, 7);
        source.RemoveRange(0, 2);
        source.Clear();

        await Assert.That(results.Data.Items).IsEmpty();
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(2);
        await Assert.That(results.Messages[1].Replaced).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[3].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task DynamicFilterStaticPredicateConstructorGuardsNullArguments()
    {
        await Assert.That(() => ObservableListEx.Filter(
                source: (null as IObservable<IChangeSet<int>>)!,
                predicate: static value => value > 0))
            .Throws<ArgumentNullException>();

        await Assert.That(() => ObservableListEx.Filter(
                source: Observable.Empty<IChangeSet<int>>(),
                predicate: (null as Func<int, bool>)!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DynamicFilterObservablePredicateConstructorGuardsNullArguments()
    {
        await Assert.That(() => ObservableListEx.Filter(
                source: (null as IObservable<IChangeSet<int>>)!,
                predicate: Observable.Return<Func<int, bool>>(static value => value > 0)))
            .Throws<ArgumentNullException>();

        await Assert.That(() => ObservableListEx.Filter(
                source: Observable.Empty<IChangeSet<int>>(),
                predicate: (null as IObservable<Func<int, bool>>)!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task FilterWithPredicateStateRejectsInvalidPolicy()
    {
        await Assert.That(() => ObservableListEx.Filter(
                source: Observable.Empty<IChangeSet<int>>(),
                predicateState: Observable.Return(0),
                predicate: static (state, value) => value == state,
                filterPolicy: (ListFilterPolicy)42))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task FilterWithPredicateStateIndexedRangeInsertionUpdatesFollowingFilteredIndexes()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<int>(0);
        using var results = source
            .Filter(predicateState, static (state, value) => value >= state)
            .AsAggregator();

        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.AddRange, new[] { 1, 3 }, 0)
        });
        source.OnNext(new ChangeSet<int>
        {
            new(ListChangeReason.AddRange, new[] { 2 }, 1)
        });
        predicateState.OnNext(2);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
    }

    [Test]
    public async Task SwitchClearsPreviousItemsAndIgnoresSupersededSignals()
    {
        using var outer = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<int>>>();
        using var first = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var second = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var results = ObservableListEx.Switch(outer).AsAggregator();

        outer.OnNext(first);
        first.OnNext(new ChangeSet<int> { new(ListChangeReason.AddRange, new[] { 1, 2 }, 0) });
        outer.OnNext(second);
        first.OnNext(new ChangeSet<int> { new(ListChangeReason.Add, 99, 0) });
        second.OnNext(new ChangeSet<int> { new(ListChangeReason.Add, 3, 0) });

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 3 });
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Clear);
        await Assert.That(results.Messages[2].Adds).IsEqualTo(1);
    }

    [Test]
    public async Task SwitchCompletionWaitsForActiveInnerCompletion()
    {
        using var outer = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<int>>>();
        using var inner = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var subscription = ObservableListEx.Switch(outer).RecordListItems(out var results);

        outer.OnNext(inner);
        outer.OnCompleted();

        await Assert.That(results.HasCompleted).IsFalse();

        inner.OnCompleted();

        await Assert.That(results.HasCompleted).IsTrue();
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task SwitchPropagatesOnlyActiveInnerErrors()
    {
        using var outer = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<int>>>();
        using var first = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var second = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        var ignored = new InvalidOperationException("ignored");
        var active = new InvalidOperationException("active");
        using var subscription = ObservableListEx.Switch(outer).RecordListItems(out var results);

        outer.OnNext(first);
        outer.OnNext(second);
        first.OnError(ignored);
        second.OnError(active);

        await Assert.That(results.Error).IsSameReferenceAs(active);
    }

    [Test]
    public async Task ToObservableChangeSetItemOverloadsGuardNullSources()
    {
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<int>)!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<int>)!, expireAfter: static _ => (TimeSpan?)null))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<int>)!, limitSizeTo: 1))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<int>)!, expireAfter: null, limitSizeTo: 1))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ToObservableChangeSetSequenceOverloadsGuardNullSources()
    {
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<IEnumerable<int>>)!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet(source: (null as IObservable<IEnumerable<int>>)!, limitSizeTo: 1))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet<int>(source: (null as IObservable<IEnumerable<int>>)!, expireAfter: static _ => (TimeSpan?)null))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ObservableListEx.ToObservableChangeSet<int>(source: (null as IObservable<IEnumerable<int>>)!, expireAfter: null, limitSizeTo: 1))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ToObservableChangeSetSizeLimitAdjustsQueuedExpirationsAndScheduledExpiration()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<int>();
        var scheduler = new TestScheduler();
        using var subscription = source
            .ToObservableChangeSet(
                expireAfter: static value => value switch
                {
                    1 => TimeSpan.FromSeconds(10),
                    2 => TimeSpan.FromSeconds(20),
                    3 => TimeSpan.FromSeconds(30),
                    _ => null
                },
                limitSizeTo: 3,
                scheduler: scheduler)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        source.OnNext(4);

        await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 2, 3, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.RecordedChangeSets[results.RecordedChangeSets.Count - 1].Removes).IsEqualTo(1);

        scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);

        await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 2, 3, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);

        await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 3, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        scheduler.AdvanceTo(TimeSpan.FromSeconds(30).Ticks);

        await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task ToObservableChangeSetSequenceOverloadEmitsInitialEmptyChangeSetAndCompletes()
    {
        using var subscription = Observable.Empty<IEnumerable<int>>()
            .ToObservableChangeSet()
            .RecordListItems(out var results);

        await Assert.That(results.RecordedChangeSets).HasSingleItem();
        await Assert.That(results.RecordedChangeSets[0].Count).IsEqualTo(0);
        await Assert.That(results.HasCompleted).IsTrue();
        await Assert.That(results.Error).IsNull();
    }

    private sealed class BoundaryItem(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
    }
}
