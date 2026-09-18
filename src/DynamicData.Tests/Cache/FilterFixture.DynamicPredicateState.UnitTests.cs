namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicateState
    {
        [InheritsTests]
        public sealed class UnitTests
            : Base
        {
            [Test]
            [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task ChangesAreMadeBeforeInitialPredicateStateValue_ItemsAreExcluded(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                // UUT Initialization
                using var subscription = source
                    .Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: Observable.Never<object>(),
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                // Add changes
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = false },
                    new Item() { Id = 4, IsIncluded = false }
                });

                // Refresh changes, with no item mutations.
                source.Refresh();

                // Refresh changes, with item mutations affecting filtering.
                foreach (var item in source.Items)
                    item.IsIncluded = !item.IsIncluded;
                source.Refresh();

                // Remove changes
                source.RemoveKeys(new[] { 2, 3 });

                // Update changes, not affecting filtering
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = false },
                    new Item() { Id = 4, IsIncluded = true }
                });

                // Update changes, affecting filtering
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false }
                });

                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(6).Because("6 source operations were performed");
                else
                    await Assert.That(results.RecordedChangeSets).IsEmpty().Because("empty changesets should be suppressed");
                await Assert.That(results.RecordedItemsByKey).IsEmpty().Because("the predicate has not initialized");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            public async Task PredicateIsNull_ThrowsException()
                => await Assert.That(() => ObservableCacheEx.Filter(
                        source: Observable.Return(ChangeSet<Item, int>.Empty),
                        predicate: null!,
                        predicateState: Observable.Empty<object>())).Throws<ArgumentNullException>();

            [Test]
            [Arguments(StreamCompletionStrategy.Asynchronous)]
            [Arguments(StreamCompletionStrategy.Immediate)]
            public async Task PredicateStateCompletesAfterInitialValue_CompletionWaitsForSourceCompletion(StreamCompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                var predicateState = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<object>()
                    : Observable.Return(new object());

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateState is ReactiveUI.Primitives.Signals.Signal<object> subject)
                {
                    subject.OnNext(new());
                    subject.OnCompleted();
                }

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial predicate, was published");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
                await Assert.That(results.HasCompleted).IsFalse().Because("changes could still be generated by the source");

                // UUT Action
                source.Complete();

                await Assert.That(results.Error).IsNull();
                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("no source operations were performed");
                else
                    await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.HasCompleted).IsTrue().Because("all source streams have completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            [Arguments(StreamCompletionStrategy.Immediate, EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Immediate, EmptyChangesetPolicy.SuppressEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Asynchronous, EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Asynchronous, EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task PredicateStateCompletesBeforeInitialValue_CompletionPropagatesIfEmptyChangesetsAreSuppressed(
                StreamCompletionStrategy completionStrategy,
                EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var predicateState = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<object>()
                    : Observable.Empty<object>();

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: predicateState,
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateState is ReactiveUI.Primitives.Signals.Signal<object> subject)
                    subject.OnCompleted();

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    await Assert.That(results.HasCompleted).IsFalse().Because("additional empty changesets can occur");
                else
                    await Assert.That(results.HasCompleted).IsTrue().Because("only empty changesets can occur");
            }

            [Test]
            [Arguments(StreamCompletionStrategy.Asynchronous)]
            [Arguments(StreamCompletionStrategy.Immediate)]
            public async Task PredicateStateFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var error = new Exception("Test");

                var predicateState = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<object>()
                    : Observable.Throw<object>(error);

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (predicateState is ReactiveUI.Primitives.Signals.Signal<object> subject)
                    subject.OnError(error);

                await Assert.That(results.Error).IsEqualTo(error).Because("errors should propagate downstream");
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            }

            [Test]
            public async Task PredicateStateIsNull_ThrowsException()
                => await Assert.That(() => ObservableCacheEx.Filter(
                        source: Observable.Return(ChangeSet<Item, int>.Empty),
                        predicate: static (object _, Item item) => item.IsIncluded,
                        predicateState: null!)).Throws<ArgumentNullException>();

            [Test]
            [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task PredicateStateChanges_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);
                using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<int>(0x5);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                // UUT Initialization
                using var subscription = source.Connect()
                    .Filter(
                        predicate: Item.FilterByIdInclusionMask,
                        predicateState: predicateState,
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(item => Item.FilterByIdInclusionMask(predicateState.Value, item))).Because("all matching items should have propagated");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                predicateState.OnNext(0xA);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 predicate change occurred");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(item => Item.FilterByIdInclusionMask(predicateState.Value, item))).Because("newly-matching items should have been added, and newly-excluded items should have been removed");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            [Arguments(StreamCompletionStrategy.Asynchronous, EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Asynchronous, EmptyChangesetPolicy.SuppressEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Immediate, EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(StreamCompletionStrategy.Immediate, EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task SourceCompletesWhenEmpty_CompletionPropagatesWhenEmptyChangesetsAreSuppressed(
                StreamCompletionStrategy completionStrategy,
                EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                // UUT Initialization & Action
                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: Observable.Concat(
                            Observable.Return(new object()),
                            Observable.Never<object>()),
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    source.Complete();

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    await Assert.That(results.HasCompleted).IsFalse().Because("the source has completed, but further empty changesets can occur");
                else
                    await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed, and no further changesets can occur");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            [Arguments(StreamCompletionStrategy.Asynchronous)]
            [Arguments(StreamCompletionStrategy.Immediate)]
            public async Task SourceCompletesWhenNotEmpty_CompletionWaitsForPredicateChangedCompletion(StreamCompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<object>(new object());

                // UUT Initialization & Action
                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    source.Complete();

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
                await Assert.That(results.HasCompleted).IsFalse().Because("the collection could still change due to new predicates");

                // UUT Action
                predicateState.OnCompleted();

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("no changes should have been made");
                await Assert.That(results.HasCompleted).IsTrue().Because("all source streams have completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            public async Task SubscriptionIsDisposed_SubscriptionDisposalPropagates()
            {
                // Setup
                using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();
                using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<object>(new());

                // UUT Initialization
                using var subscription = source
                    .Filter(
                        predicate: static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                subscription.Dispose();

                await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should propagate to all sources");
                await Assert.That(predicateState.HasObservers).IsFalse().Because("subscription disposal should propagate to all sources");
            }

            protected override IObservable<IChangeSet<Item, int>> BuildUut(
                    IObservable<IChangeSet<Item, int>> source,
                    Func<Item, bool> predicate,
                    bool suppressEmptyChangeSets)
                => source.Filter(
                    predicate: (_, item) => predicate.Invoke(item),
                    predicateState: Observable.Return(new object()),
                    suppressEmptyChangeSets: suppressEmptyChangeSets);
        }
    }
}
