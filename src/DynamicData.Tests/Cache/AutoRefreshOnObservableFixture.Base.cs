using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Reactive.Testing;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshOnObservableFixture
{
    public abstract class Base
    {
        [Fact]
        public void ChangeSetBufferIsGiven_ReevaluatorNotificationsAreBufferedOnScheduler()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });

            var scheduler = new TestScheduler();


            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    reevaluator:        Item.ObserveValueChanged,
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (publish reevaluator notification)
            ++item2.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the reevaluator notification should have been buffered");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            

            // UUT Action (advance time, within buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(5).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the buffer window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            
            // UUT Action (advance time, to buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "a buffer window expired");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published a reevaluator notification");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published a reevaluator notification");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published a reevaluator notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            

            // UUT Action (publish reevaluator notification)
            ++item1.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the reevaluator notification should have been buffered");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (advance time, within buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the buffer window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (publish additional reevaluator notification)
            ++item3.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the reevaluator notification should have been buffered");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (advance time, to buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "a buffer window expired");
            results.RecordedChangeSets.Skip(2).First().Count.Should().Be(2, "2 items published a reevaluator notification");
            results.RecordedChangeSets.Skip(2).First().Refreshes.Should().Be(2, "2 items published a reevaluator notification");
            results.RecordedChangeSets.Skip(2).First().Select(change => change.Current).Should().BeEquivalentTo(new[] { item1, item3 }, "items #2 and #3 published reevaluator notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (normal refresh)
            source.Refresh(item2);
            
            // Normal refreshes should not be buffered
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "one source operation was performed");
            results.RecordedChangeSets.Skip(3).First().Count.Should().Be(1, "1 item was refreshed, within the source");
            results.RecordedChangeSets.Skip(3).First().Refreshes.Should().Be(1, "1 item was refreshed, within the source");
            results.RecordedChangeSets.Skip(3).First().First().Current.Should().Be(item2, "item #2 was refreshed, within the source");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ChangeSetBufferIsGiven_RemoveDuringWindow_DropsPendingRefresh()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            source.AddOrUpdate(item1);

            var scheduler = new TestScheduler();


            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    reevaluator:        Item.ObserveValueChanged,
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.RecordedChangeSets.Count.Should().Be(1, "the initial Add changeset should propagate");


            // UUT Action (publish reevaluator notification at T=5)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(5).Ticks);
            ++item1.Value;


            // UUT Action (remove the item at T=9, before the buffer window ends at T=15)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(9).Ticks);
            source.Remove(item1);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "the Remove should propagate immediately");
            results.RecordedChangeSets.Skip(1).First().Removes.Should().Be(1, "the source removed item #1");


            // UUT Action (advance past the buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty(
                "a Refresh for a key the source has already removed is incoherent on arrival");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ChangeSetBufferIsGiven_UpdateDuringWindow_RefreshEmittedForNewInstance()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1V1 = new Item() { Id = 1 };
            using var item1V2 = new Item() { Id = 1 };
            source.AddOrUpdate(item1V1);

            var scheduler = new TestScheduler();


            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    reevaluator:        Item.ObserveValueChanged,
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.RecordedChangeSets.Count.Should().Be(1, "the initial Add changeset should propagate");


            // UUT Action (v1's reevaluator fires at T=5)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(5).Ticks);
            ++item1V1.Value;


            // UUT Action (Update replaces v1 with v2 at T=9, within the window armed by the v1 refresh)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(9).Ticks);
            source.AddOrUpdate(item1V2);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "the Update should propagate immediately");


            // UUT Action (v2's reevaluator fires at T=12, arming a fresh window for T=22)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(12).Ticks);
            ++item1V2.Value;


            // UUT Action (advance to T=15, the original v1-armed window boundary)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);

            results.RecordedChangeSets.Skip(2).Should().BeEmpty(
                "the v1-armed window must not fire after the Update replaced its pending refresh");


            // UUT Action (advance past the new buffer window at T=22)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(22).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "the v2 buffer window has expired");
            results.RecordedChangeSets.Skip(2).First().Refreshes.Should().Be(1, "exactly one Refresh is buffered for the key");
            results.RecordedChangeSets.Skip(2).First().First().Current.Should().BeSameAs(item1V2, "a Refresh carries the instance the source currently holds, not a superseded one");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ChangeSetBufferIsGiven_MultipleUpdatesDuringWindow_OnlyLatestRefreshEmitted()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1V1 = new Item() { Id = 1 };
            using var item1V2 = new Item() { Id = 1 };
            using var item1V3 = new Item() { Id = 1 };
            source.AddOrUpdate(item1V1);

            var scheduler = new TestScheduler();


            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    reevaluator:        Item.ObserveValueChanged,
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.RecordedChangeSets.Count.Should().Be(1, "the initial Add changeset should propagate");


            // UUT Action (v1 reEval at T=2, then Update to v2 at T=4)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(2).Ticks);
            ++item1V1.Value;

            scheduler.AdvanceTo(TimeSpan.FromSeconds(4).Ticks);
            source.AddOrUpdate(item1V2);


            // UUT Action (v2 reEval at T=6, then Update to v3 at T=8)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(6).Ticks);
            ++item1V2.Value;

            scheduler.AdvanceTo(TimeSpan.FromSeconds(8).Ticks);
            source.AddOrUpdate(item1V3);


            // UUT Action (v3 reEval at T=10)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);
            ++item1V3.Value;

            results.RecordedChangeSets.Skip(1).Count().Should().Be(2, "two Updates propagated immediately");


            // UUT Action (advance past v3's window at T=20)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "the chain produces a single Refresh changeset");
            results.RecordedChangeSets.Skip(3).First().Refreshes.Should().Be(1, "the chain coalesces to a single Refresh");
            results.RecordedChangeSets.Skip(3).First().First().Current.Should().BeSameAs(item1V3, "a Refresh carries the instance the source currently holds, not a superseded one");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ChangeSetBufferIsGiven_SourceCompletesBeforeWindowExpires_PendingRefreshIsEmittedBeforeCompletion()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item = new Item() { Id = 1 };
            source.AddOrUpdate(item);

            var scheduler = new TestScheduler();


            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    reevaluator:        Item.ObserveValueChanged,
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);


            // UUT Action (reevaluator fires at T=5, arms the buffer window for T=15)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(5).Ticks);
            ++item.Value;


            // UUT Action (complete the source and the reevaluator while the buffer window is still open)
            source.Complete();
            item.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.SelectMany(static cs => cs).Where(static c => c.Reason is ChangeReason.Refresh).Should().HaveCount(1,
                "a pending buffered refresh must surface before completion, even when source and reevaluator have already completed");
            results.HasCompleted.Should().BeTrue(
                "completion of all upstream subscriptions triggers an immediate flush of pending refreshes");


            // UUT Action (advance past the original window boundary; the timer was cancelled when sources completed)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);

            results.RecordedChangeSets.SelectMany(static cs => cs).Where(static c => c.Reason is ChangeReason.Refresh).Should().HaveCount(1,
                "the pending timer was disposed when sources completed; no second refresh should fire at the window boundary");
        }

        [Fact]
        public void NoChangeSetBuffer_AddAndRemoveInSameChangeset_NoRefreshEmitted()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };


            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValue)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);


            // UUT Action (Add + Remove in a single changeset)
            source.Edit(updater =>
            {
                updater.AddOrUpdate(item1);
                updater.Remove(item1);
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.SelectMany(static cs => cs).Where(static c => c.Reason is ChangeReason.Refresh).Should().BeEmpty(
                "a sync reevaluator emission queued during Add must not surface once the same drain removes the item");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void NoChangeSetBuffer_AddAndUpdateInSameChangeset_NoRefreshFromObsoleteInstance()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1V1 = new Item() { Id = 1 };
            using var item1V2 = new Item() { Id = 1 };


            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValue)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);


            // UUT Action (Add v1 + Update to v2 in a single changeset)
            source.Edit(updater =>
            {
                updater.AddOrUpdate(item1V1);
                updater.AddOrUpdate(item1V2);
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.SelectMany(static cs => cs).Where(static c => c.Reason is ChangeReason.Refresh).Should().BeEmpty(
                "a Refresh carrying an instance the source has already replaced is incoherent on arrival");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void NoChangeSetBuffer_MultipleUpdatesInSameChangeset_NoRefreshFromIntermediateInstances()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1V1 = new Item() { Id = 1 };
            using var item1V2 = new Item() { Id = 1 };
            using var item1V3 = new Item() { Id = 1 };


            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValue)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);


            // UUT Action (Add v1 + Update v2 + Update v3 in a single changeset)
            source.Edit(updater =>
            {
                updater.AddOrUpdate(item1V1);
                updater.AddOrUpdate(item1V2);
                updater.AddOrUpdate(item1V3);
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.SelectMany(static cs => cs).Where(static c => c.Reason is ChangeReason.Refresh).Should().BeEmpty(
                "every Refresh carrying a value the source has already superseded is incoherent on arrival");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ItemIsAdded_SubscribesToReevaluator()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.AddOrUpdate(new[] { item1, item2, item3 });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            item1.HasObservers.Should().BeTrue("the reevaluator should be invoked and subscribed to, for each added item");
            item2.HasObservers.Should().BeTrue("the reevaluator should be invoked and subscribed to, for each added item");
            item3.HasObservers.Should().BeTrue("the reevaluator should be invoked and subscribed to, for each added item");
        }
        
        [Fact]
        public void ItemIsMoved_NotificationPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            var items = new[] { item1, item2, item3 };
            
            var initialChangeset = new ChangeSet<Item, int>()
            {
                new Change<Item, int>(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
                new Change<Item, int>(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1),
                new Change<Item, int>(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 2)
            };

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Prepend(initialChangeset),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "3 items were added to the source");
            results.RecordedItemsSorted.Should().BeEquivalentTo(
                items,
                options => options.WithStrictOrdering(),
                "item indexes should propagate");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.OnNext(new ChangeSet<Item, int>()
            {
                new Change<Item, int>(
                    key:            item3.Id,
                    current:        item3,
                    currentIndex:   0,
                    previousIndex:  2) 
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "an item was moved within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            results.RecordedItemsSorted.Should().BeEquivalentTo(
                new[] { item3, item1, item2 },
                options => options.WithStrictOrdering(),
                "an item was moved within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
        
        [Fact]
        public void ItemIsRefreshed_NotificationPropagates()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            var reevaluatorInvocationCount = 0;
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    item =>
                    {
                        ++reevaluatorInvocationCount;
                        return Item.ObserveValueChanged(item);
                    })
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            reevaluatorInvocationCount.Should().Be(3, "the reevaluator should be invoked and subscribed to, for each added item");

            // UUT Action
            source.Refresh(item2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item was refreshed within the source");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item was refreshed within the source");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 was refreshed within the source");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            reevaluatorInvocationCount.Should().Be(3, "the reevaluator should only be invoked for items being added to the collection.");
        }

        [Fact]
        public void ItemIsRemoved_UnsubscribesFromReevaluator()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            source.Remove(item2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "1 item was removed from the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            item2.HasObservers.Should().BeFalse("removing an item should trigger unsubscription from its reevaluator");
            item1.HasObservers.Should().BeTrue("the item was not removed from the source");
            item3.HasObservers.Should().BeTrue("the item was not removed from the source");
        }

        [Fact]
        public void ItemIsUpdated_ReInvokesReevaluator()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            using var item4 = new Item() { Id = 2 };
            source.AddOrUpdate(item4);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "1 item was replaced within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            item2.HasObservers.Should().BeFalse("replacing an item should trigger unsubscription from its reevaluator");
            item4.HasObservers.Should().BeTrue("adding an item should invoke its reevaluator and subscribe to it");
            item1.HasObservers.Should().BeTrue("the item was not removed from the source");
            item3.HasObservers.Should().BeTrue("the item was not removed from the source");
            
            
            // UUT Action (updated item publishes reevaluator notification)
            ++item4.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(2).First().Count.Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(2).First().Refreshes.Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(2).First().First().Current.Should().Be(item4, "item #4 published a reevaluation notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
            
        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void ReevaluatorCompletesWhenNotOnlyItemInSource_CompletionWaitsForSourceCompletionAndOtherReevaluatorCompletions(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization & Action (initial completion)
            if (notificationStrategy is NotificationStrategy.Immediate)
                item2.Complete();

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                item2.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("not all notification sources have completed");


            // UUT Action (remaining reevaluator completions)
            item1.Complete();
            item3.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            
            // UUT Action (source completion)
            source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeTrue("all notification sources have completed");
        }
            
        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void ReevaluatorCompletesWhenOnlyItemInSource_CompletionWaitsForSourceCompletion(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item = new Item() { Id = 1 };
            
            source.AddOrUpdate(item);
            

            // UUT Initialization & Action (reevaluator completion)
            if (notificationStrategy is NotificationStrategy.Immediate)
                item.Complete();

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                item.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "1 item was added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action (source completion)
            source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeTrue("all notification sources have completed");
        }
            
        [Fact]
        public void ReevaluatorEmitsAsynchronously_ItemRefreshes()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            ++item2.Value;

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published a reevaluation notification");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published a reevaluation notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
          
        [Fact]
        public void ReevaluatorEmitsImmediately_ItemDoesNotRefresh()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization & Action
            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValue)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedChangeSets[0].Refreshes.Should().Be(0, "re-evaluation notifications should be ignored within the initial subscription frame");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
            
        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void ReevaluatorFails_ErrorPropagates(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            var error = new Exception("Test");
            

            // UUT Initialization & Action
            if (notificationStrategy is NotificationStrategy.Immediate)
                item2.SetError(error);

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                item2.SetError(error);

            results.Error.Should().Be(error, "upstream errors should propagate downstream");
            if (notificationStrategy is NotificationStrategy.Immediate)
                results.RecordedChangeSets.Should().BeEmpty("an error occurred during processing of the initial changeset");
            else
            {
                results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            }
        }
            
        [Fact]
        public void ReevaluatorThrows_ExceptionPropagates()
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var error = new Exception("Test");


            // UUT Initialization
            using var subscription = BuildUut<Unit>(
                    source:         source.Connect(),
                    reevaluator:    _ => throw error)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no initial changesets were published");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            
            // UUT Action
            using var item = new Item() { Id = 1 };
            source.AddOrUpdate(item);

            results.Error.Should().Be(error, "upstream errors should propagate downstream");
            results.RecordedChangeSets.Should().BeEmpty("an error occurred during processing of the initial changeset");
        }
            
        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void SourceCompletesWhenEmpty_CompletionPropagates(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);


            // UUT Initialization & Action
            if (notificationStrategy is NotificationStrategy.Immediate)
                source.Complete();

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeTrue("all notification sources have completed");
        }

        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void SourceCompletesWhenNotEmpty_CompletionWaitsForReevaluatorCompletions(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            

            // UUT Initialization & Action (source completion)
            if (notificationStrategy is NotificationStrategy.Immediate)
                source.Complete();

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("not all notification sources have completed");


            // UUT Action (initial reevaluator completion)
            item2.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("not all notification sources have completed");


            // UUT Action (remaining reevaluator completions)
            item1.Complete();
            item3.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeTrue("all notification sources have completed");
        }

        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void SourceFails_ErrorPropagates(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            var error = new Exception("Test");
            

            // UUT Initialization & Action
            if (notificationStrategy is NotificationStrategy.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(
                    source:         source.Connect(),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                source.SetError(error);

            results.Error.Should().Be(error, "upstream errors should propagate downstream");
            if (notificationStrategy is NotificationStrategy.Immediate)
                results.RecordedChangeSets.Should().BeEmpty("an error occurred before the initial changeset");
            else
            {
                results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            }
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => BuildUut(
                    source:         null!,
                    reevaluator:    Item.ObserveValueChanged))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public void SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();

            using var item1 = new Item() { Id = 1 };
            using var item2 = new Item() { Id = 2 };
            using var item3 = new Item() { Id = 3 };
            
            var initialChangeset = new ChangeSet<Item, int>()
            {
                new Change<Item, int>(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new Change<Item, int>(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new Change<Item, int>(reason: ChangeReason.Add, key: item3.Id, current: item3)
            };
            

            // UUT Initialization
            using var subscription = BuildUut(
                    source:         source.Prepend(initialChangeset),
                    reevaluator:    Item.ObserveValueChanged)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            subscription.Dispose();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            source.HasObservers.Should().BeFalse("subscription disposal should propagate");
            item1.HasObservers.Should().BeFalse("subscription disposal should propagate");
            item2.HasObservers.Should().BeFalse("subscription disposal should propagate");
            item3.HasObservers.Should().BeFalse("subscription disposal should propagate");
        }

        protected abstract IObservable<IChangeSet<Item, int>> BuildUut<TAny>(
            IObservable<IChangeSet<Item, int>>  source,
            Func<Item, IObservable<TAny>>       reevaluator,
            TimeSpan?                           changeSetBuffer = null,
            IScheduler?                         scheduler       = null);
    }
}
