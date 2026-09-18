namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicate
    {
        [InheritsTests]
        public sealed class UnitTests
            : Base
        {
            [Test]
            [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task ChangesAreMadeBeforeInitialPredicateChangedValue_ItemsAreExcluded(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                // UUT Initialization
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged: Observable.Never<Func<Item, bool>>(),
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
                {
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(6).Because("6 source operations were performed");
                    await Assert.That(results.RecordedItemsByKey).IsEmpty().Because("the predicate has not initialized");
                }
                else
                {
                    await Assert.That(results.RecordedChangeSets).IsEmpty().Because("empty changesets should be suppressed");
                }
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public async Task PredicateChangedChanges_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);
                using var predicateChanged = new ReactiveUI.Primitives.Signals.StateSignal<Func<Item, bool>>(Item.FilterByIsIncluded);

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
                        predicateChanged: predicateChanged,
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                predicateChanged.OnNext(Item.FilterByEvenId);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 predicate change occurred");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByEvenId)).Because("newly-matching items should have been added, and newly-excluded items should have been removed");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            [Arguments(StreamCompletionStrategy.Asynchronous)]
            [Arguments(StreamCompletionStrategy.Immediate)]
            public async Task PredicateChangedCompletesAfterInitialValue_CompletionWaitsForSourceCompletion(StreamCompletionStrategy completionStrategy)
            {
                // Setup
                var source = new TestSourceCache<Item, int>(Item.SelectId);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                var predicateChanged = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>>()
                    : Observable.Return(Item.FilterByIsIncluded);

                var reapplyFilter = new ReactiveUI.Primitives.Signals.Signal<Unit>();

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(predicateChanged)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>> subject)
                {
                    subject.OnNext(Item.FilterByIsIncluded);
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
            public async Task PredicateChangedCompletesBeforeInitialValue_CompletionPropagatesIfEmptyChangesetsAreSuppressed(
                StreamCompletionStrategy completionStrategy,
                EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var predicateChanged = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>>()
                    : Observable.Empty<Func<Item, bool>>();

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged: predicateChanged,
                        suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>> subject)
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
            public async Task PredicateChangedFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var error = new Exception("Test");

                var predicateChanged = (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    ? new ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>>()
                    : Observable.Throw<Func<Item, bool>>(error);

                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged: predicateChanged)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>> subject)
                    subject.OnError(error);

                await Assert.That(results.Error).IsEqualTo(error).Because("errors should propagate downstream");
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            }

            [Test]
            public async Task PredicateChangedIsNull_ThrowsException()
                => await Assert.That(() => ObservableCacheEx.Filter(
                        source: Observable.Return(ChangeSet<Item, int>.Empty),
                        reapplyFilter: Observable.Never<Unit>(),
                        predicateChanged: null!)).Throws<ArgumentNullException>();

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
                        predicateChanged: Observable.Concat(
                            Observable.Return(Item.FilterByIsIncluded),
                            Observable.Never<Func<Item, bool>>()),
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

                using var predicateChanged = new ReactiveUI.Primitives.Signals.StateSignal<Func<Item, bool>>(Item.FilterByIsIncluded);

                // UUT Initialization & Action
                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(predicateChanged)
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
                predicateChanged.OnCompleted();

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("no changes should have been made");
                await Assert.That(results.HasCompleted).IsTrue().Because("all source streams have completed");

                // Final Verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Test]
            public async Task SubscriptionIsDisposed_SubscriptionDisposalPropagates()
            {
                // Setup
                using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();
                using var predicateChanged = new ReactiveUI.Primitives.Signals.StateSignal<Func<Item, bool>>(Item.FilterByIsIncluded);

                // UUT Initialization
                using var subscription = source
                    .Filter(predicateChanged)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                subscription.Dispose();

                await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should propagate to all sources");
                await Assert.That(predicateChanged.HasObservers).IsFalse().Because("subscription disposal should propagate to all sources");
            }

            protected override IObservable<IChangeSet<Item, int>> BuildUut(
                    IObservable<IChangeSet<Item, int>> source,
                    Func<Item, bool> predicate,
                    bool suppressEmptyChangeSets)
                => source.Filter(
                    predicateChanged: Observable.Return(predicate),
                    suppressEmptyChangeSets: suppressEmptyChangeSets);
        }
    }
}
