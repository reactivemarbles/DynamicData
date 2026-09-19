namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public abstract class Base
    {
        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ExcludedItemsAreRemoved_NoChangesAreMade(EmptyChangesetPolicy emptyChangesetPolicy)
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

            // UUT Initialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(source.Items.Where(static item => !item.IsIncluded).ToArray());

            await Assert.That(results.Error).IsNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("only excluded items were manipulated");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            }
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreAdded_MatchingItemsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            // UUT Intialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = true },
                new Item() { Id = 4, IsIncluded = false },
                new Item() { Id = 5, IsIncluded = false },
                new Item() { Id = 6, IsIncluded = false }
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreMoved_MovementsAreIgnored(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

            var items = new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = true }
            };

            // UUT Initialization
            using var subscription = BuildUut(
                    source: source
                        .Prepend(new ChangeSet<Item, int>(items
                            .Select((item, index) => new Change<Item, int>(
                                reason: ChangeReason.Add,
                                key: item.Id,
                                current: item,
                                index: index)))),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Moved, key: items[2].Id, items[2], previous: default, currentIndex: 0, previousIndex: 2),
                new(reason: ChangeReason.Moved, key: items[1].Id, items[1], previous: default, currentIndex: 1, previousIndex: 2)
            });

            await Assert.That(results.Error).IsNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source opreation was performed");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("no changes should have been made");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            }
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed(EmptyChangesetPolicy emptyChangesetPolicy)
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

            // UUT Initialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (add items)
            foreach (var item in source.Items)
                item.IsIncluded = true;

            source.Refresh();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await results.RecordedChangeSets.ElementAt(1).ShouldHaveRefreshed(source.Items.Take(3), "all unchanged items should have been refreshed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (remove items)
            foreach (var item in source.Items.Take(3))
                item.IsIncluded = false;

            source.Refresh();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await results.RecordedChangeSets.ElementAt(2).ShouldHaveRefreshed(source.Items.Skip(3), "all unchanged items should have been refreshed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-excluded items should have been removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        public async Task ItemsAreUpdated_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
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

            // UUT Intialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (add and update items)
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = true },
                new Item() { Id = 4, IsIncluded = true },
                new Item() { Id = 5, IsIncluded = true },
                new Item() { Id = 6, IsIncluded = true }
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (remove and update items)
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = false },
                new Item() { Id = 2, IsIncluded = false },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = true },
                new Item() { Id = 5, IsIncluded = true },
                new Item() { Id = 6, IsIncluded = true }
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-excluded items should have been removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task MatchingItemsAreRemoved_RemovalsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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

            // UUT Initialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset was published");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(source.Items.Where(Item.FilterByIsIncluded).ToArray());

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEmpty().Because("all matching items were removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        [Arguments(StreamCompletionStrategy.Asynchronous)]
        [Arguments(StreamCompletionStrategy.Immediate)]
        public async Task SourceFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
        {
            var error = new Exception("Test");

            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            if (completionStrategy is StreamCompletionStrategy.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: true)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                source.SetError(error);

            await Assert.That(results.Error).IsEqualTo(error);
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(() => BuildUut(
                    source: null!,
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: true)).Throws<ArgumentNullException>();

        protected abstract IObservable<IChangeSet<Item, int>> BuildUut(
            IObservable<IChangeSet<Item, int>> source,
            Func<Item, bool> predicate,
            bool suppressEmptyChangeSets);
    }
}
