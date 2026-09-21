using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public static partial class FilterFixture
{
    public class Static
    {
        private const int IdentitySeed = 0x1165;

        public Static(ITestOutputHelper output)
            => output.WriteLine($"Bogus seed: {IdentitySeed}");

        /// <summary>
        /// Indexed list changes address one occurrence, even when values are equal or share the same reference.
        /// Refresh, replacement, movement, and removal must preserve the other slots' identities and inclusion states.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EqualItemsHaveIndexedChanges_OnlyTheSpecifiedSlotChanges(bool useSameReference)
        {
            // Arrange: seed the list with two equal occurrences before subscribing.
            var randomizer = new Randomizer(IdentitySeed);
            using var source = new TestSourceList<Item>();
            var first = new Item { Id = randomizer.Int(), IsIncluded = true };
            var second = useSameReference ? first : first with { };
            var replacement = new Item { Id = randomizer.Int(), IsIncluded = true };
            Assert.Equal(first, second);

            source.AddRange(new[] { first, second });

            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            // Assert the initial snapshot retains both occurrences.
            Assert.Null(results.Error);
            Assert.Collection(results.RecordedItems,
                item => Assert.Same(first, item),
                item => Assert.Same(second, item));

            // Act: re-evaluate only the second slot, even when both slots reference the mutated object.
            second.IsIncluded = false;
            source.Refresh(1);

            // Assert: the first slot retains its prior inclusion state until notified.
            Assert.Null(results.Error);
            Assert.Same(first, Assert.Single(results.RecordedItems));

            // Act: replace the excluded slot with an included item.
            source.Edit(items => items[1] = replacement);

            // Assert: the new reference is inserted without replacing the untouched occurrence.
            Assert.Null(results.Error);
            Assert.Collection(results.RecordedItems,
                item => Assert.Same(first, item),
                item => Assert.Same(replacement, item));

            // Act: move the replacement ahead of the original occurrence.
            source.Edit(items => items.Move(1, 0));

            // Assert: both exact references follow their indexed positions.
            Assert.Null(results.Error);
            Assert.Collection(results.RecordedItems,
                item => Assert.Same(replacement, item),
                item => Assert.Same(first, item));

            // Act: remove the original occurrence at its new position.
            source.RemoveAt(1);

            // Assert: only the replacement remains.
            Assert.Null(results.Error);
            Assert.Same(replacement, Assert.Single(results.RecordedItems));

            // Act: refresh the remaining slot after excluding it.
            replacement.IsIncluded = false;
            source.Refresh(0);

            // Assert: all notified exclusions have propagated.
            Assert.Null(results.Error);
            Assert.Empty(results.RecordedItems);
        }

        [Fact]
        public void DuplicateItemsAreAdded_ItemsAreTrackedSeparately()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "there were initial items to publish");
            results.RecordedItems.Should().BeEquivalentTo(new[] { 2, 4, 2 },
                because: "all matching items should have been added",
                config: options => options.WithoutStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Remove(2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().ContainSingle("an operation was performed upon an included item");
            results.RecordedItems.Should().BeEquivalentTo(new[] { 4, 2 },
                because: "only one of the duplicate items was removed",
                config: options => options.WithoutStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");

        
            // UUT Action
            source.Remove(3);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("an operation was performed upon an excluded item");
            results.HasCompleted.Should().BeFalse("the source has not completed");

        
            // UUT Action
            source.Remove(2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().ContainSingle("an operation was performed upon an included item");
            results.RecordedItems.Should().BeEquivalentTo(new[] { 4 },
                because: "the second duplicate item was removed",
                config: options => options.WithoutStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
        
        [Fact]
        public void ExcludedItemIsAdded_NoChangesAreMade()
        {
            // Setup
            using var source = new TestSourceList<Item>();


            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to publish");
            results.RecordedItems.Should().BeEmpty("no items have been added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Add(new Item() { Id = 1, IsIncluded = false });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
        
        [Fact]
        public void ExcludedItemIsRemoved_NoChangesAreMade()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.RemoveAt(5);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ExcludedItemsAreRemoved_NoChangesAreMade()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.RemoveMany(source.Items.Where(static item => !item.IsIncluded).ToArray());

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("empty changesets should be suppressed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ItemsAreAdded_MatchingItemsPropagate()
        {
            // Setup
            using var source = new TestSourceList<Item>();


            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to publish");
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
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ItemsAreMoved_MatchingMovementsPropagate()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
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
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
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
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("empty changesets should be suppressed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ItemsAreRefreshed_ItemsAreReFilteredOrRefreshed()
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

        [Fact]
        public void ItemsAreReplaced_ItemsAreReFiltered()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have propagated",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all newly-matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all newly-excluded items should have been removed",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void MatchingItemIsAdded_ItemPropagates()
        {
            // Setup
            using var source = new TestSourceList<Item>();


            // UUT Initialization
            using var subscription = source.Connect()
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to publish");
            results.RecordedItems.Should().BeEmpty("no items have been added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Add(new Item() { Id = 1, IsIncluded = true });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items, "the matching item should have been added");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
        
        [Fact]
        public void MatchingItemIsRemoved_RemovalPropagates()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "all matching items should have been added",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            var removedItem = source.Items[2];
            source.RemoveAt(2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().ContainSingle("1 source operation was performed");
            results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded),
                because: "a matching item was removed",
                config: options => options.WithStrictOrdering());
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void MatchingItemsAreRemoved_RemovalsPropagate()
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

        [Fact]
        public void PredicateIsNull_ThrowsException()
            => FluentActions.Invoking(static () => ObservableListEx.Filter(
                    source:     Observable.Empty<IChangeSet<Item>>(),
                    predicate:  null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(SourceType.Asynchronous)]
        [InlineData(SourceType.Immediate)]
        public void SourceCompletes_CompletionPropagates(SourceType sourceType)
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial item should have been published");
            results.RecordedItems.Should().BeEquivalentTo(source.Items, "the initial item should have been published");
            results.HasCompleted.Should().BeTrue("the source has completed");
        }

        [Theory]
        [InlineData(SourceType.Asynchronous)]
        [InlineData(SourceType.Immediate)]
        public void SourceFails_ErrorPropagates(SourceType sourceType)
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

            results.Error.Should().Be(error);
            if (sourceType is SourceType.Asynchronous)
            {
                results.RecordedChangeSets.Count.Should().Be(1, "the initial item should have been published");
                results.RecordedItems.Should().BeEquivalentTo(source.Items, "the initial item should have been published");
            }
            else
            {
                results.RecordedChangeSets.Should().BeEmpty("an error occurred during initialization");
            }
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(static () => ObservableListEx.Filter<Item>(
                    source:     null!,
                    predicate:  Item.FilterByIsIncluded))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public void SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item>>();


            // UUT Initialization
            using var subscription = source
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no initial changeset occurred");
            results.RecordedItems.Should().BeEmpty("the source has not initialized");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            subscription.Dispose();

            source.HasObservers.Should().BeFalse("subscription disposal should propagate upstream");
        }
    }
}
