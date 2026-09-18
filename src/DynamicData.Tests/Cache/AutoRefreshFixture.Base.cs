namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    public abstract class Base
    {
        [Test]
        public async Task ChangeSetBufferIsGiven_PropertyChangedNotificationsAreBufferedOnScheduler()
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
                    source: source.Connect(),
                    changeSetBuffer: TimeSpan.FromSeconds(10),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish property change notification)
            ++item2.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("the property change notification should have been buffered");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time, within buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(5).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("the buffer window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time, to buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("a buffer window expired");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Count).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Refreshes).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().First().Current).IsEqualTo(item2).Because("item #2 published a property change notification");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items should have changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish property change notification)
            ++item1.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the property change notification should have been buffered");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time, within buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the buffer window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish additional property change notification)
            ++item3.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the property change notification should have been buffered");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time, to buffer window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("a buffer window expired");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Count).IsEqualTo(2).Because("2 items published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Refreshes).IsEqualTo(2).Because("2 items published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Select(change => change.Current)).IsEquivalentTo(new[] { item1, item3 }).Because("items #2 and #3 published property change notification");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items should have changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (normal refresh)
            source.Refresh(item2);

            // Normal refreshes should not be buffered
            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(3).Count()).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedChangeSets.Skip(3).First().Count).IsEqualTo(1).Because("1 item was refreshed, within the source");
            await Assert.That(results.RecordedChangeSets.Skip(3).First().Refreshes).IsEqualTo(1).Because("1 item was refreshed, within the source");
            await Assert.That(results.RecordedChangeSets.Skip(3).First().First().Current).IsEqualTo(item2).Because("item #2 was refreshed, within the source");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items should have changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemIsAdded_SubscribesToPropertyChanged()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };

            source.AddOrUpdate(new[] { item1, item2, item3 });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            await Assert.That(item1.HasSubscriptions).IsTrue().Because("the PropertyChanged event should be subscribed to, for each added item");
            await Assert.That(item2.HasSubscriptions).IsTrue().Because("the PropertyChanged event should be subscribed to, for each added item");
            await Assert.That(item3.HasSubscriptions).IsTrue().Because("the PropertyChanged event should be subscribed to, for each added item");
        }

        [Test]
        public async Task ItemIsMoved_NotificationPropagates()
        {
            // Setup
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };

            var items = new[] { item1, item2, item3 };

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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("3 items were added to the source");
            await Assert.That(results.RecordedItemsSorted).IsEquivalentTo(items).Because("item indexes should propagate");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.OnNext(new ChangeSet<Item, int>()
            {
                new Change<Item, int>(
                    key:            item3.Id,
                    current:        item3,
                    currentIndex:   0,
                    previousIndex:  2)
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("an item was moved within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
            await Assert.That(results.RecordedItemsSorted).IsEquivalentTo(new[] { item3, item1, item2 }).Because("an item was moved within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemIsRefreshed_NotificationPropagates()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Refresh(item2);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Count).IsEqualTo(1).Because("1 item was refreshed within the source");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Refreshes).IsEqualTo(1).Because("1 item was refreshed within the source");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().First().Current).IsEqualTo(item2).Because("item #2 was refreshed within the source");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items were changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task ItemIsRemoved_UnsubscribesFromPropertyChanged()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            source.Remove(item2);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("1 item was removed from the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            await Assert.That(item2.HasSubscriptions).IsFalse().Because("removing an item should trigger unsubscription from its reevaluator");
            await Assert.That(item1.HasSubscriptions).IsTrue().Because("the item was not removed from the source");
            await Assert.That(item3.HasSubscriptions).IsTrue().Because("the item was not removed from the source");
        }

        [Test]
        public async Task ItemIsUpdated_ReSubscribesToPropertyChanged()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            var item4 = new Item() { Id = 2 };
            source.AddOrUpdate(item4);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("1 item was replaced within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            await Assert.That(item2.HasSubscriptions).IsFalse().Because("replacing an item should trigger unsubscription from its reevaluator");
            await Assert.That(item4.HasSubscriptions).IsTrue().Because("adding an item should invoke its reevaluator and subscribe to it");
            await Assert.That(item1.HasSubscriptions).IsTrue().Because("the item was not removed from the source");
            await Assert.That(item3.HasSubscriptions).IsTrue().Because("the item was not removed from the source");
        }

        [Test]
        public async Task PropertyChangedOccurs_ItemRefreshes()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            ++item2.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Count).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Refreshes).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().First().Current).IsEqualTo(item2).Because("item #2 published a property change notification");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task PropertyChangeThrottleIsGiven_PropertyChangedNotificationsAreThrottledByScheduler()
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
                    source: source.Connect(),
                    propertyChangeThrottle: TimeSpan.FromSeconds(10),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish property change notification)
            ++item2.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("the throttle window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish additional property change notification, immediately)
            ++item2.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("the throttle window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time to end of throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("the throttle window ended");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Count).IsEqualTo(1).Because("1 item published property change notifications");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Refreshes).IsEqualTo(1).Because("1 item published property change notifications");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().First().Current).IsEqualTo(item2).Because("item #2 published property change notifications");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items should have changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish property change notification)
            ++item2.Value;

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the throttle window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (publish additional property change notification, within throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(15).Ticks);
            ++item2.Value;
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the throttle window has not yet ended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time to end of original throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(20).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2)).IsEmpty().Because("the throttle window should have been extended");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action (advance time to end of throttle window)
            scheduler.AdvanceTo(TimeSpan.FromSeconds(25).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("the throttle window ended");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Count).IsEqualTo(1).Because("1 item published property change notifications");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Refreshes).IsEqualTo(1).Because("1 item published property change notifications");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().First().Current).IsEqualTo(item2).Because("item #2 published property change notifications");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items should have changed, within the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(NotificationStrategy.Immediate)]
        [Arguments(NotificationStrategy.Asynchronous)]
        public async Task SourceCompletesWhenEmpty_CompletionPropagates(NotificationStrategy notificationStrategy)
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsTrue().Because("all notification sources have completed");
        }

        [Test]
        [Arguments(NotificationStrategy.Immediate)]
        [Arguments(NotificationStrategy.Asynchronous)]
        public async Task SourceCompletesWhenNotEmpty_CompletionDoesNotPropagate(NotificationStrategy notificationStrategy)
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("PropertyChanged events can still publish notifications");
        }

        [Test]
        [Arguments(NotificationStrategy.Immediate)]
        [Arguments(NotificationStrategy.Asynchronous)]
        public async Task SourceFails_ErrorPropagates(NotificationStrategy notificationStrategy)
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

            await Assert.That(results.Error).IsEqualTo(error).Because("upstream errors should propagate downstream");
            if (notificationStrategy is NotificationStrategy.Immediate)
                await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred before the initial changeset");
            else
            {
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            }
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(() => BuildUut(source: null!)).Throws<ArgumentNullException>();

        [Test]
        public async Task SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(new[] { item1, item2, item3 }).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            subscription.Dispose();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should propagate");
            await Assert.That(item1.HasSubscriptions).IsFalse().Because("subscription disposal should propagate");
            await Assert.That(item2.HasSubscriptions).IsFalse().Because("subscription disposal should propagate");
            await Assert.That(item3.HasSubscriptions).IsFalse().Because("subscription disposal should propagate");
        }

        protected abstract IObservable<IChangeSet<Item, int>> BuildUut(
            IObservable<IChangeSet<Item, int>> source,
            TimeSpan? changeSetBuffer = null,
            TimeSpan? propertyChangeThrottle = null,
            IScheduler? scheduler = null);
    }
}
