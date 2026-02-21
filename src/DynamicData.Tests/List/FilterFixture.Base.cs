using System;
using System.Linq;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public static partial class FilterFixture
{
    public abstract class Base
    {
        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ExcludedItemsAreRemoved_NoChangesAreMade(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.RemoveMany(source.Items.Where(static item => !item.IsIncluded).ToArray());

            results.Error.Should().BeNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                    because: "only excluded items were manipulated",
                    config: options => options.WithStrictOrdering());
            }
            else
            {
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            }
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreAdded_MatchingItemsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new TestSourceList<Item>();


            // UUT Intialization
            using var subscription = BuildUut(
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEmpty("no items have been added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreMoved_MatchingMovementsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items,
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action: Moves for matching items, 
            source.Edit(items =>
            {
                items.Move(2, 0);
                items.Move(1, 5);
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source opreation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been moved, accordingly",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action: Moves for excluded items
            source.Edit(items =>
            {
                items.Move(4, 1);
                items.Move(3, 5);
            });

            results.Error.Should().BeNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source opreation was performed");
                results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                    because: "no matching items were moved",
                    config: options => options.WithStrictOrdering());
            }
            else
            {
                results.RecordedChangeSets.Skip(2).Should().BeEmpty("empty changesets should be suppressed");
            }
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (add items)
            foreach (var item in source.Items)
                item.IsIncluded = true;

            source.Refresh();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedChangeSets.ElementAt(1).ShouldHaveRefreshed(source.Items.Take(3), "all unchanged items should have been refreshed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (remove items)
            foreach (var item in source.Items.Take(3))
                item.IsIncluded = false;

            source.Refresh();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedChangeSets.ElementAt(2).ShouldHaveRefreshed(source.Items.Skip(3), "all unchanged items should have been refreshed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-excluded items should have been removed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        public void ItemsAreReplaced_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have propagated",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all newly-matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all newly-excluded items should have been removed",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void MatchingItemsAreRemoved_RemovalsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have propagated",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.RemoveMany(source.Items.Where(Item.FilterByIsIncluded).ToArray());

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEmpty("all matching items were removed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Theory]
        [InlineData(SourceType.Asynchronous)]
        [InlineData(SourceType.Immediate)]
        public void SourceFails_ErrorPropagates(SourceType sourceType)
        {
            using var source = new TestSourceList<Item>();

            var error = new Exception("Test");

            if (sourceType is SourceType.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    true)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            if (sourceType is SourceType.Asynchronous)
                source.SetError(error);

            results.Error.Should().Be(error);
            if (sourceType is SourceType.Asynchronous)
            {
                results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
                results.RecordedItems.Should().BeEmpty("no source items were added");
            }
            else
            {
                results.RecordedChangeSets.Should().BeEmpty("an error occurred during initialization");
            }
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => BuildUut(
                    source:                     null!,
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    true))
                .Should()
                .Throw<ArgumentNullException>();

        protected abstract IObservable<IChangeSet<Item>> BuildUut(
            IObservable<IChangeSet<Item>>   source,
            Func<Item, bool>                predicate,
            bool                            suppressEmptyChangeSets);
    }
}
