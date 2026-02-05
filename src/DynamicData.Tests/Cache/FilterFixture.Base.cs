using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Remove(source.Items.Where(static item => !item.IsIncluded).ToArray());

            results.Error.Should().BeNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "only excluded items were manipulated");
            }
            else
            {
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            }
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreAdded_MatchingItemsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);


            // UUT Intialization
            using var subscription = BuildUut(
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreMoved_MovementsAreIgnored(EmptyChangesetPolicy emptyChangesetPolicy)
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();

            var items = new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = true }
            };

            // UUT Initialization
            using var subscription = BuildUut(
                    source:                     source
                        .Prepend(new ChangeSet<Item, int>(items
                            .Select((item, index) => new Change<Item, int>(
                                reason:     ChangeReason.Add,
                                key:        item.Id,
                                current:    item,
                                index:      index)))),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "all matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Moved, key: items[2].Id, items[2], previous: default, currentIndex: 0, previousIndex: 2),
                new(reason: ChangeReason.Moved, key: items[1].Id, items[1], previous: default, currentIndex: 1, previousIndex: 2)
            });

            results.Error.Should().BeNull();
            if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
            {
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source opreation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "no changes should have been made");
            }
            else
            {
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            }
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (add items)
            foreach (var item in source.Items)
                item.IsIncluded = true;

            source.Refresh();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedChangeSets.ElementAt(1).ShouldHaveRefreshed(source.Items.Take(3), "all unchanged items should have been refreshed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (remove items)
            foreach (var item in source.Items.Take(3))
                item.IsIncluded = false;

            source.Refresh();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedChangeSets.ElementAt(2).ShouldHaveRefreshed(source.Items.Skip(3), "all unchanged items should have been refreshed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-excluded items should have been removed");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        public void ItemsAreUpdated_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-matching items should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all newly-excluded items should have been removed");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
        [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
        public void MatchingItemsAreRemoved_RemovalsPropagate(EmptyChangesetPolicy emptyChangesetPolicy)
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
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Remove(source.Items.Where(Item.FilterByIsIncluded).ToArray());

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEmpty("all matching items were removed");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Theory]
        [InlineData(CompletionStrategy.Asynchronous)]
        [InlineData(CompletionStrategy.Immediate)]
        public void SourceFails_ErrorPropagates(CompletionStrategy completionStrategy)
        {
            var error = new Exception("Test");

            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            if (completionStrategy is CompletionStrategy.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(
                    source:                     source.Connect(),
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    true)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (completionStrategy is CompletionStrategy.Asynchronous)
                source.SetError(error);

            results.Error.Should().Be(error);
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => BuildUut(
                    source:                     null!,
                    predicate:                  Item.FilterByIsIncluded,
                    suppressEmptyChangeSets:    true))
                .Should()
                .Throw<ArgumentNullException>();

        protected abstract IObservable<IChangeSet<Item, int>> BuildUut(
            IObservable<IChangeSet<Item, int>>  source,
            Func<Item, bool>                    predicate,
            bool                                suppressEmptyChangeSets);
    }
}
