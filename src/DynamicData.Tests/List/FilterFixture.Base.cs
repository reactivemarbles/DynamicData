namespace DynamicData.Tests.List;

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
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
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
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
                await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("only excluded items were manipulated");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("empty changesets should be suppressed");
            }
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreAdded_MatchingItemsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new TestSourceList<Item>();

            // UUT Intialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
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
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreMoved_MatchingMovementsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action: Moves for matching items,
            source.Edit(items =>
            {
                items.Move(2, 0);
                items.Move(1, 5);
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source opreation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all matching items should have been moved, accordingly");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action: Moves for excluded items
            source.Edit(items =>
            {
                items.Move(4, 1);
                items.Move(3, 5);
            });

            await Assert.That(results.Error).IsNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source opreation was performed");
                await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("no matching items were moved");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("empty changesets should be suppressed");
            }
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed(EmptyChangesetPolicy emptyChangesetPolicy)
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
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
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
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        public async Task ItemsAreReplaced_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
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

            // UUT Intialization
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
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
                source.ReplaceAt(0, new Item() { Id = 1, IsIncluded = true });
                source.ReplaceAt(1, new Item() { Id = 2, IsIncluded = true });
                source.ReplaceAt(2, new Item() { Id = 3, IsIncluded = true });
                source.ReplaceAt(3, new Item() { Id = 4, IsIncluded = true });
                source.ReplaceAt(4, new Item() { Id = 5, IsIncluded = true });
                source.ReplaceAt(5, new Item() { Id = 6, IsIncluded = true });
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all newly-matching items should have been added");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (remove and replace items)
            source.Edit(items =>
            {
                source.ReplaceAt(0, new Item() { Id = 1, IsIncluded = false });
                source.ReplaceAt(1, new Item() { Id = 2, IsIncluded = false });
                source.ReplaceAt(2, new Item() { Id = 3, IsIncluded = false });
                source.ReplaceAt(3, new Item() { Id = 4, IsIncluded = true });
                source.ReplaceAt(4, new Item() { Id = 5, IsIncluded = true });
                source.ReplaceAt(5, new Item() { Id = 6, IsIncluded = true });
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all newly-excluded items should have been removed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [Arguments(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public async Task MatchingItemsAreRemoved_RemovalsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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
            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
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
        [Arguments(SourceType.Asynchronous)]
        [Arguments(SourceType.Immediate)]
        public async Task SourceFails_ErrorPropagates(SourceType sourceType)
        {
            using var source = new TestSourceList<Item>();

            var error = new Exception("Test");

            if (sourceType is SourceType.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(
                    source: source.Connect(),
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: true)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            if (sourceType is SourceType.Asynchronous)
                source.SetError(error);

            await Assert.That(results.Error).IsEqualTo(error);
            if (sourceType is SourceType.Asynchronous)
            {
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
                await Assert.That(results.RecordedItems).IsEmpty().Because("no source items were added");
            }
            else
            {
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred during initialization");
            }
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(() => BuildUut(
                    source: null!,
                    predicate: Item.FilterByIsIncluded,
                    suppressEmptyChangeSets: true))
                .Throws<ArgumentNullException>();

        protected abstract IObservable<IChangeSet<Item>> BuildUut(
            IObservable<IChangeSet<Item>> source,
            Func<Item, bool> predicate,
            bool suppressEmptyChangeSets);
    }
}
