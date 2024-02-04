using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using Bogus;
using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;
using System.Reactive.Disposables;

namespace DynamicData.Tests.Cache;

public static partial class ExpireAfterFixture
{
    public sealed class ForStream
    {
        [Fact]
        public void ExpiredItemIsRemoved_RemovalIsSkipped()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3 }, "item #1 should have been removed");

            // Send a notification to remove an item that's already been removed
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Remove, key: item1.Id, current: item1),
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Skip(2).Should().BeEmpty("no changes should have occurred");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
            });
            scheduler.AdvanceBy(1);

            var item4 = new Item() { Id = 4 };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item4.Id, current: item4)
            });
            scheduler.AdvanceBy(1);

            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Remove, key: item2.Id, current: item2)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(3, "3 source operations were performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item3, item4 }, "3 items were added, and one was removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item4 }, "items #1 and #3 should have been removed");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            // Extend the expiration to a later time
            var item2 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item2.Id, current: item2, previous: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(2, "2 source operations were performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2 }, "item #1 was added, and then replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(2).Should().BeEmpty("no expirations should have occurred");

            // Shorten the expiration to an earlier time
            var item3 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item3.Id, current: item3, previous: item2)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item3 }, "item #1 was replaced");

            // One more update with no changes to the expiration
            var item4 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item4.Id, current: item4, previous: item3)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Skip(3).Count().Should().Be(1, "1 source operation was performed.");
            results.Data.Items.Should().BeEquivalentTo(new[] { item4 }, "item #1 was replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEmpty("item #1 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(5).Should().BeEmpty("no expirations should have occurred");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    pollingInterval: TimeSpan.FromMilliseconds(20),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
                new(reason: ChangeReason.Add, key: item4.Id, current: item4),
                new(reason: ChangeReason.Add, key: item5.Id, current: item5)
            });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new Item() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new Item() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item6.Id, current: item6),
                new(reason: ChangeReason.Add, key: item7.Id, current: item7)
            });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new Item() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item8.Id, current: item8)
            });
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new Item() { Id = 9 };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item9.Id, current: item9)
            });
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item10.Id, current: item10, previous: item4)
            });
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item11.Id, current: item11, previous: item5)
            });
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Refresh, key: item3.Id, current: item3)
            });
            scheduler.AdvanceBy(1);


            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(7, "7 source operations were performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "9 items were added, 2 were replaced, and 1 was refreshed");

            // Item scheduled to expire at 10ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(7).Should().BeEmpty("no changes should have occurred");

            // Item scheduled to expire at 15ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(7).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(7).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #1, #2, #6, #7, and #8 should have been removed");

            // Item scheduled to expire at 30ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(8).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(8).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");

            // Item scheduled to expire at 45ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(9).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(9).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have been removed");

            // Expired items should be polled, but none should be found
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(10).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(10).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have been removed");

            // Next poll should not find anything to expire.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(11).Should().BeEmpty("no changes should have occurred");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact(Skip = "Existing defect, very minor defect, items defined to never expire actually do, at DateTimeOffset.MaxValue")]
        public void PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
                new(reason: ChangeReason.Add, key: item4.Id, current: item4),
                new(reason: ChangeReason.Add, key: item5.Id, current: item5)
            });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new Item() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new Item() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item6.Id, current: item6),
                new(reason: ChangeReason.Add, key: item7.Id, current: item7)
            });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new Item() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item8.Id, current: item8)
            });
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new Item() { Id = 9 };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item9.Id, current: item9)
            });
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item10.Id, current: item10, previous: item4)
            });
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Update, key: item11.Id, current: item11, previous: item5)
            });
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Refresh, key: item3.Id, current: item3)
            });
            scheduler.AdvanceBy(1);


            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(7, "7 source operations were performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "11 items were added, 2 were replaced, and 1 was refreshed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(7).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item8, item9, item10, item11 }, "item #1 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(8).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item9, item10, item11 }, "item #8 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(9).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #2, #6, and #7 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(10).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(11).Should().BeEmpty("no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(12).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(12).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have expired");

            // Remaining item should never expire
            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(13).Should().BeEmpty("no changes should have occurred");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact(Skip = "Existing defect, completion does not wait")]
        public void RemovalsArePending_CompletionWaitsForRemovals()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3)
            });
            scheduler.AdvanceBy(1);

            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.Messages.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3 }, "item #1 should have been removed");

            source.OnCompleted();

            results.Error.Should().BeNull();
            results.IsCompleted.Should().BeFalse("removals are pending");
            results.Messages.Skip(2).Should().BeEmpty("no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.IsCompleted.Should().BeTrue();
            results.Messages.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEquivalentTo(new[] { item2 }, "item #3 should have expired");

            results.IsCompleted.Should().BeFalse();
        }

        // Covers https://github.com/reactivemarbles/DynamicData/issues/716
        [Fact(Skip = "Existing defect, removals are skipped when scheduler invokes early")]
        public void SchedulerIsInaccurate_RemovalsAreNotSkipped()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = new FakeScheduler()
            {
                Now = DateTimeOffset.FromUnixTimeMilliseconds(0)
            };

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });


            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

            // Simulate the scheduler invoking all actions 1ms early.
            while(scheduler.ScheduledActions.Count is not 0)
            {
                if (scheduler.ScheduledActions[0].DueTime is DateTimeOffset dueTime)
                    scheduler.Now = dueTime - TimeSpan.FromMilliseconds(1);

                scheduler.ScheduledActions[0].Invoke();
                scheduler.ScheduledActions.RemoveAt(0);
            }

            results.Error.Should().BeNull();
            results.Messages.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.Data.Items.Should().BeEmpty("item #1 should have been removed");

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void SourceCompletes_CompletionIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3)
            });

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            source.OnCompleted();

            results.Error.Should().BeNull();
            results.Messages.Skip(1).Should().BeEmpty("no changes should have occurred");

            results.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void SourceCompletesImmediately_CompletionIsPropagated()
        {
            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };

            var source = Observable.Create<IChangeSet<Item, int>>(observer =>
            {
                observer.OnNext(new ChangeSet<Item, int>()
                {
                    new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                    new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                    new(reason: ChangeReason.Add, key: item3.Id, current: item3)
                });

                observer.OnCompleted();

                return Disposable.Empty;
            });

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operation was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            results.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void SourceErrors_ErrorIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: item => item.Expiration - scheduler.Now,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Count.Should().Be(1, "1 source operations was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

            var error = new Exception("This is a test");
            source.OnError(error);

            results.Error.Should().Be(error);
            results.Messages.Skip(1).Should().BeEmpty("no changes should have occurred");
            results.IsCompleted.Should().BeFalse();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Messages.Skip(1).Should().BeEmpty("notifications should not get published after an error");
        }

        [Fact]
        public void SourceErrorsImmediately_ErrorIsPropagated()
        {
            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };

            var error = new Exception("This is a test");

            var source = Observable.Create<IChangeSet<Item, int>>(observer =>
            {
                observer.OnNext(new ChangeSet<Item, int>()
                {
                    new(reason: ChangeReason.Add, key: item1.Id, current: item1)
                });

                observer.OnError(error);

                return Disposable.Empty;
            });

            var scheduler = CreateTestScheduler();

            using var results = source
                .ExpireAfter(
                    timeSelector: item => item.Expiration - scheduler.Now,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            results.Error.Should().Be(error);
            results.Messages.Count.Should().Be(1, "1 source operations was performed");
            results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");
            results.IsCompleted.Should().BeFalse();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Messages.Skip(1).Should().BeEmpty("notifications should not get published after an error");
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.ExpireAfter(
                source: (null as IObservable<IChangeSet<Item, int>>)!,
                timeSelector: static _ => default))
            .Should().Throw<ArgumentNullException>();

        [Fact(Skip = "Existing defect, operator does not properly handle items with a null timeout, when using a real scheduler, it passes a TimeSpan to the scheduler that is outside of the supported range")]
        public async Task ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = ThreadPoolScheduler.Instance;

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var maxExpiration = PublishStressChangeSets(
                source: source,
                scheduler: scheduler,
                stressCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(10),
                maxItemLifetime: TimeSpan.FromMilliseconds(50),
                maxChangeCount: 10);

            await Observable.Timer(maxExpiration + TimeSpan.FromMilliseconds(100), scheduler);

            results.Error.Should().BeNull();
            results.Messages.SelectMany(static changeSet => changeSet.Where(change => change.Reason is ChangeReason.Remove)).Should().AllSatisfy(static change => change.Current.Expiration.Should().NotBeNull("only items with an expiration should have expired"));
            results.Data.Items.Should().AllSatisfy(item => item.Expiration.Should().BeNull("all items with an expiration should have expired"));

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task ThreadPoolSchedulerIsUsedWithPolling_ExpirationIsThreadSafe()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = ThreadPoolScheduler.Instance;

            using var results = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    pollingInterval: TimeSpan.FromMilliseconds(10),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var maxExpiration = PublishStressChangeSets(
                source: source,
                scheduler: scheduler,
                stressCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(10),
                maxItemLifetime: TimeSpan.FromMilliseconds(50),
                maxChangeCount: 10);

            await Observable.Timer(maxExpiration + TimeSpan.FromMilliseconds(100), scheduler);

            var now = scheduler.Now;

            results.Error.Should().BeNull();
            results.Data.Items.Should().AllSatisfy(item => item.Expiration.Should().BeNull("all items with an expiration should have expired"));

            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void TimeSelectorIsNull_ThrowsException()
            => FluentActions.Invoking(() => new Subject<IChangeSet<Item, int>>().ExpireAfter(
                timeSelector: null!))
            .Should().Throw<ArgumentNullException>();

        [Fact(Skip = "Exsiting defect, errors are re-thrown instead of propagated, user code is not protected")]
        public void TimeSelectorThrows_SubscriptionReceivesError()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var scheduler = CreateTestScheduler();

            var error = new Exception("This is a test.");

            using var results = source
                .ExpireAfter(
                    timeSelector: _ => throw error,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .AsAggregator();

            var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.Messages.Should().BeEmpty("no source operations should have been processed");
            results.IsCompleted.Should().BeFalse();
        }

        private static DateTimeOffset PublishStressChangeSets(
            IObserver<IChangeSet<Item, int>> source,
            IScheduler scheduler,
            int stressCount,
            TimeSpan minItemLifetime,
            TimeSpan maxItemLifetime,
            int maxChangeCount)
        {
            var nextItemId = 1;
            var randomizer = new Randomizer(1234567);
            var maxExpiration = DateTimeOffset.MinValue;

            var cache = new ChangeAwareCache<Item, int>();

            for (var i = 0; i < stressCount; ++i)
            {
                var changeCount = randomizer.Int(1, maxChangeCount);

                for (var j = 0; j < changeCount; ++j)
                {
                    var changeReason = (cache.Count is 0)
                        ? ChangeReason.Add
                        : randomizer.Enum(exclude: ChangeReason.Moved);

                    if (changeReason is ChangeReason.Add)
                    {
                        var item = new Item()
                        {
                            Id = nextItemId++,
                            Expiration = GenerateExpiration()
                        };

                        cache.AddOrUpdate(item, item.Id);
                        continue;
                    }

                    var key = randomizer.CollectionItem((ICollection<int>)cache.Keys);

                    switch (changeReason)
                    {
                        case ChangeReason.Refresh:
                            cache.Refresh(key);
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(key);
                            break;

                        case ChangeReason.Update:
                            var item = new Item()
                            {
                                Id = key,
                                Expiration = GenerateExpiration()
                            };

                            cache.AddOrUpdate(item, item.Id);
                            break;
                    }
                }

                source.OnNext(cache.CaptureChanges());
            }

            return maxExpiration;

            DateTimeOffset? GenerateExpiration()
            {
                if (randomizer.Bool())
                    return null;

                var expiration = scheduler.Now + randomizer.TimeSpan(minItemLifetime, maxItemLifetime);
                if (expiration > maxExpiration)
                    maxExpiration = expiration;

                return expiration;
            }
        }
    }
}
