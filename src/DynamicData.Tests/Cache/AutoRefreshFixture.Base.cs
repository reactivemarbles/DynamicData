using FluentAssertions;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    public abstract class Base
    {
        [Fact]
        public void ChangeSetBufferIsGiven_PropertyChangedNotificationsAreBufferedOnScheduler()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });

            var scheduler = new TestScheduler();

            // UUT Initialization
            using var subscription = BuildUut(
                    source:             source.Connect(),
                    changeSetBuffer:    TimeSpan.FromSeconds(10),
                    scheduler:          scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (publish property change notification)
            ++item2.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the property change notification should have been buffered");
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
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published a property change notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            // UUT Action (publish property change notification)
            ++item1.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the property change notification should have been buffered");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (advance time, within buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the buffer window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (publish additional property change notification)
            ++item3.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the property change notification should have been buffered");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (advance time, to buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "a buffer window expired");
            results.RecordedChangeSets.Skip(2).First().Count.Should().Be(2, "2 items published a property change notification");
            results.RecordedChangeSets.Skip(2).First().Refreshes.Should().Be(2, "2 items published a property change notification");
            results.RecordedChangeSets.Skip(2).First().Select(change => change.Current).Should().BeEquivalentTo(new[] { item1, item3 }, "items #2 and #3 published property change notification");
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
        public void ItemIsAdded_SubscribesToPropertyChanged()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action
            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };

            source.AddOrUpdate(new[] { item1, item2, item3 });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            item1.HasSubscriptions.Should().BeTrue("the PropertyChanged event should be subscribed to, for each added item");
            item2.HasSubscriptions.Should().BeTrue("the PropertyChanged event should be subscribed to, for each added item");
            item3.HasSubscriptions.Should().BeTrue("the PropertyChanged event should be subscribed to, for each added item");
        }
        
        [Fact]
        public void ItemIsMoved_NotificationPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            var items = new [] { item1, item2, item3 };
            
            var initialChangeset = new ChangeSet<Item, int>()
            {
                new Change<Item, int>(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
                new Change<Item, int>(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1),
                new Change<Item, int>(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 2)
            };

            // UUT Initialization
            using var subscription = BuildUut(source.Prepend(initialChangeset))
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

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action
            source.Refresh(item2);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item was refreshed within the source");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item was refreshed within the source");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 was refreshed within the source");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void ItemIsRemoved_UnsubscribesFromPropertyChanged()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
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
            
            item2.HasSubscriptions.Should().BeFalse("removing an item should trigger unsubscription from its reevaluator");
            item1.HasSubscriptions.Should().BeTrue("the item was not removed from the source");
            item3.HasSubscriptions.Should().BeTrue("the item was not removed from the source");
        }

        [Fact]
        public void ItemIsUpdated_ReSubscribesToPropertyChanged()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action
            var item4 = new Item() { Id = 2 };
            source.AddOrUpdate(item4);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "1 item was replaced within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            item2.HasSubscriptions.Should().BeFalse("replacing an item should trigger unsubscription from its reevaluator");
            item4.HasSubscriptions.Should().BeTrue("adding an item should invoke its reevaluator and subscribe to it");
            item1.HasSubscriptions.Should().BeTrue("the item was not removed from the source");
            item3.HasSubscriptions.Should().BeTrue("the item was not removed from the source");
        }
            
        [Fact]
        public void PropertyChangedOccurs_ItemRefreshes()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
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
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published a property change notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }

        [Fact]
        public void PropertyChangeThrottleIsGiven_PropertyChangedNotificationsAreThrottledByScheduler()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });

            var scheduler = new TestScheduler();

            // UUT Initialization
            using var subscription = BuildUut(
                    source:                 source.Connect(),
                    propertyChangeThrottle: TimeSpan.FromSeconds(10),
                    scheduler:              scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (publish property change notification)
            ++item2.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the throttle window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (publish additional property change notification, immediately)
            ++item2.Value;

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the throttle window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (advance time to end of throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "the throttle window ended");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published property change notifications");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published property change notifications");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published property change notifications");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            // UUT Action (publish property change notification)
            ++item2.Value;
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the throttle window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action (publish additional property change notification, within throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);
            ++item2.Value;
            scheduler.AdvanceBy(1);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the throttle window has not yet ended");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            // UUT Action (advance time to end of original throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("the throttle window should have been extended");
            results.HasCompleted.Should().BeFalse("the source has not completed");
            
            // UUT Action (advance time to end of throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(25).Ticks);
            
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "the throttle window ended");
            results.RecordedChangeSets.Skip(2).First().Count.Should().Be(1, "1 item published property change notifications");
            results.RecordedChangeSets.Skip(2).First().Refreshes.Should().Be(1, "1 item published property change notifications");
            results.RecordedChangeSets.Skip(2).First().First().Current.Should().Be(item2, "item #2 published property change notifications");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items should have changed, within the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");
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

            using var subscription = BuildUut(source.Connect())
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
        public void SourceCompletesWhenNotEmpty_CompletionDoesNotPropagate(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            // UUT Initialization & Action (source completion)
            if (notificationStrategy is NotificationStrategy.Immediate)
                source.Complete();

            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (notificationStrategy is NotificationStrategy.Asynchronous)
                source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("PropertyChanged events can still publish notifications");
        }

        [Theory]
        [InlineData(NotificationStrategy.Immediate)]
        [InlineData(NotificationStrategy.Asynchronous)]
        public void SourceFails_ErrorPropagates(NotificationStrategy notificationStrategy)
        {
            // Setup 
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            source.AddOrUpdate(new[] { item1, item2, item3 });
            
            var error = new Exception("Test");
            
            // UUT Initialization & Action
            if (notificationStrategy is NotificationStrategy.Immediate)
                source.SetError(error);

            using var subscription = BuildUut(source.Connect())
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
            => FluentActions.Invoking(() => BuildUut(source: null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public void SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            
            var initialChangeset = new ChangeSet<Item, int>()
            {
                new Change<Item, int>(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new Change<Item, int>(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new Change<Item, int>(reason: ChangeReason.Add, key: item3.Id, current: item3)
            };
            
            // UUT Initialization
            using var subscription = BuildUut(source.Prepend(initialChangeset))
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
            item1.HasSubscriptions.Should().BeFalse("subscription disposal should propagate");
            item2.HasSubscriptions.Should().BeFalse("subscription disposal should propagate");
            item3.HasSubscriptions.Should().BeFalse("subscription disposal should propagate");
        }

        protected abstract IObservable<IChangeSet<Item, int>> BuildUut(
            IObservable<IChangeSet<Item, int>>  source,
            TimeSpan?                           changeSetBuffer         = null,
            TimeSpan?                           propertyChangeThrottle  = null,
            IScheduler?                         scheduler               = null);
    }
}
