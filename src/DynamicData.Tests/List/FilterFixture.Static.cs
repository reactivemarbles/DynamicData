namespace DynamicData.Tests.List;

public static partial class FilterFixture
{
    public class Static
    {
        [Test]
        public async Task DuplicateItemsAreAdded_ItemsAreTrackedSeparately()
        {
            // Setup
            using var source = new TestSourceList<int>();

            source.AddRange(new[]
            {
                1,
                2,
                3,
                4,
                3,
                2
            });

            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(static item => (item % 2) is 0)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("there were initial items to publish");
            await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 2, 4, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(2);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).HasSingleItem().Because("an operation was performed upon an included item");
            await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 4, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("only one of the duplicate items was removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(3);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("an operation was performed upon an excluded item");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(2);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).HasSingleItem().Because("an operation was performed upon an included item");
            await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { 4 }, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the second duplicate item was removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ExcludedItemIsAdded_NoChangesAreMade()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to publish");
            await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Add(new Item() { Id = 1, IsIncluded = false });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ExcludedItemIsRemoved_NoChangesAreMade()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.RemoveAt(5);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ExcludedItemsAreRemoved_NoChangesAreMade()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.RemoveMany(source.Items.Where(static item => !item.IsIncluded).ToArray());

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemsAreAdded_MatchingItemsPropagate()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to publish");
            await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.AddRange(new[]
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
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemsAreMoved_MatchingMovementsPropagate()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action: Moves for matching items,
            source.Edit(items =>
            {
                items.Move(2, 0);
                items.Move(1, 5);
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been moved, accordingly");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action: Moves for excluded items
            source.Edit(items =>
            {
                items.Move(4, 1);
                items.Move(3, 5);
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("empty changesets should be suppressed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (add items)
            foreach (var item in source.Items)
                item.IsIncluded = true;

            source.Refresh();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await results.RecordedChangeSets.ElementAt(1).ShouldHaveRefreshed(source.Items.Take(3), "all unchanged items should have been refreshed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (remove items)
            foreach (var item in source.Items.Take(3))
                item.IsIncluded = false;

            source.Refresh();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await results.RecordedChangeSets.ElementAt(2).ShouldHaveRefreshed(source.Items.Skip(3), "all unchanged items should have been refreshed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded)).Because("all newly-excluded items should have been removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemsAreReplaced_ItemsAreReFiltered()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (add and replace items)
            source.Edit(items =>
            {
                items[0] = new Item() { Id = 1, IsIncluded = true };
                items[1] = new Item() { Id = 2, IsIncluded = true };
                items[2] = new Item() { Id = 3, IsIncluded = true };
                items[3] = new Item() { Id = 4, IsIncluded = true };
                items[4] = new Item() { Id = 5, IsIncluded = true };
                items[5] = new Item() { Id = 6, IsIncluded = true };
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all newly-matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (remove and replace items)
            source.Edit(items =>
            {
                items[0] = new Item() { Id = 1, IsIncluded = false };
                items[1] = new Item() { Id = 2, IsIncluded = false };
                items[2] = new Item() { Id = 3, IsIncluded = false };
                items[3] = new Item() { Id = 4, IsIncluded = true };
                items[4] = new Item() { Id = 5, IsIncluded = true };
                items[5] = new Item() { Id = 6, IsIncluded = true };
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all newly-excluded items should have been removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task MatchingItemIsAdded_ItemPropagates()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to publish");
            await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Add(new Item() { Id = 1, IsIncluded = true });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items).Because("the matching item should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task MatchingItemIsRemoved_RemovalPropagates()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            var removedItem = source.Items[2];
            source.RemoveAt(2);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).HasSingleItem().Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("a matching item was removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task MatchingItemsAreRemoved_RemovalsPropagate()
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
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
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have propagated");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.RemoveMany(source.Items.Where(Item.FilterByIsIncluded).ToArray());

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEmpty().Because("all matching items were removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task PredicateIsNull_ThrowsException()
            => await Assert.That(static () => ObservableListEx.Filter(
                    source: Observable.Empty<IChangeSet<Item>>(),
                    predicate: null!))
                .Throws<ArgumentNullException>();

        [Test]
        [Arguments(SourceType.Asynchronous)]
        [Arguments(SourceType.Immediate)]
        public async Task SourceCompletes_CompletionPropagates(SourceType sourceType)
        {
            // Setup
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
            {
                new Item() { Id = 1, IsIncluded = true }
            });

            if (sourceType is SourceType.Immediate)
                source.Complete();

            // UUT Initialization & Action
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            if (sourceType is SourceType.Asynchronous)
                source.Complete();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial item should have been published");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items).Because("the initial item should have been published");
            await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");
        }

        [Test]
        [Arguments(SourceType.Asynchronous)]
        [Arguments(SourceType.Immediate)]
        public async Task SourceFails_ErrorPropagates(SourceType sourceType)
        {
            using var source = new TestSourceList<Item>();

            source.AddRange(new[]
            {
                new Item() { Id = 1, IsIncluded = true }
            });

            var error = new Exception("Test");

            if (sourceType is SourceType.Immediate)
                source.SetError(error);

            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            if (sourceType is SourceType.Asynchronous)
                source.SetError(error);

            await Assert.That(results.Error).IsEqualTo(error);
            if (sourceType is SourceType.Asynchronous)
            {
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial item should have been published");
                await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items).Because("the initial item should have been published");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred during initialization");
            }
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(static () => ObservableListEx.Filter<Item>(
                    source: null!,
                    predicate: Item.FilterByIsIncluded))
                .Throws<ArgumentNullException>();

        [Test]
        public async Task SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            // UUT Initialization
            using var subscription = source
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no initial changeset occurred");
            await Assert.That(results.RecordedItems).IsEmpty().Because("the source has not initialized");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            subscription.Dispose();

            await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should propagate upstream");
        }
    }
}
