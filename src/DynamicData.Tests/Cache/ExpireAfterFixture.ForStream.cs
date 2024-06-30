using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using Bogus;
using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class ExpireAfterFixture
{
    public sealed class ForStream
    {
        [Fact]
        public void ExpiredItemIsRemoved_RemovalIsSkipped()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3 }, "item #1 should have been removed");

            // Send a notification to remove an item that's already been removed
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Remove, key: item1.Id, current: item1),
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3),
            });
            scheduler.AdvanceBy(1);

            var item4 = new TestItem() { Id = 4 };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item4.Id, current: item4)
            });
            scheduler.AdvanceBy(1);

            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Remove, key: item2.Id, current: item2)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(3, "3 source operations were performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item3, item4 }, "3 items were added, and one was removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item4 }, "items #1 and #3 should have been removed");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            // Extend the expiration to a later time
            var item2 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item2.Id, current: item2, previous: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(2, "2 source operations were performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2 }, "item #1 was added, and then replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("no expirations should have occurred");

            // Shorten the expiration to an earlier time
            var item3 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item3.Id, current: item3, previous: item2)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item3 }, "item #1 was replaced");

            // One more update with no changes to the expiration
            var item4 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item4.Id, current: item4, previous: item3)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item4 }, "item #1 was replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEmpty("item #1 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(5).Should().BeEmpty("no expirations should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    pollingInterval: TimeSpan.FromMilliseconds(20),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 2),
                new(reason: ChangeReason.Add, key: item4.Id, current: item4, index: 3),
                new(reason: ChangeReason.Add, key: item5.Id, current: item5, index: 4)
            });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item6.Id, current: item6, index: 5),
                new(reason: ChangeReason.Add, key: item7.Id, current: item7, index: 6)
            });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item8.Id, current: item8, index: 7)
            });
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new TestItem() { Id = 9 };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item9.Id, current: item9, index: 8)
            });
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item10.Id, current: item10, previous: item4, currentIndex: 3, previousIndex: 3)
            });
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item11.Id, current: item11, previous: item5, currentIndex: 4, previousIndex: 4)
            });
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Refresh, key: item3.Id, current: item3, index: 2)
            });
            scheduler.AdvanceBy(1);

            // Move changes should be ignored completely.
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Moved, key: item3.Id, current: item3, previous: default, currentIndex: 3, previousIndex: 2),
                new(reason: ChangeReason.Moved, key: item1.Id, current: item1, previous: default, currentIndex: 1, previousIndex: 0),
                new(reason: ChangeReason.Moved, key: item1.Id, current: item1, previous: default, currentIndex: 4, previousIndex: 1)
            });


            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(7, "8 source operations were performed, and 1 should have been ignored");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "9 items were added, 2 were replaced, and 1 was refreshed");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Item scheduled to expire at 10ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(7).Should().BeEmpty("no changes should have occurred");

            // Item scheduled to expire at 15ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(7).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(7).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #1, #2, #6, #7, and #8 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Item scheduled to expire at 30ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(8).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(8).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Item scheduled to expire at 45ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(9).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(9).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Expired items should be polled, but none should be found
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(10).Should().BeEmpty("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(10).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Next poll should not find anything to expire.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(11).Should().BeEmpty("no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 2),
                new(reason: ChangeReason.Add, key: item4.Id, current: item4, index: 3),
                new(reason: ChangeReason.Add, key: item5.Id, current: item5, index: 4)
            });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item6.Id, current: item6, index: 5),
                new(reason: ChangeReason.Add, key: item7.Id, current: item7, index: 6)
            });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item8.Id, current: item8, index: 7)
            });
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new TestItem() { Id = 9 };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item9.Id, current: item9, index: 8)
            });
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item10.Id, current: item10, previous: item4, currentIndex: 3, previousIndex: 3)
            });
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Update, key: item11.Id, current: item11, previous: item5, currentIndex: 4, previousIndex: 4)
            });
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Refresh, key: item3.Id, current: item3, index: 2)
            });
            scheduler.AdvanceBy(1);

            // Move changes should be ignored completely.
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Moved, key: item3.Id, current: item3, previous: default, currentIndex: 3, previousIndex: 2),
                new(reason: ChangeReason.Moved, key: item1.Id, current: item1, previous: default, currentIndex: 1, previousIndex: 0),
                new(reason: ChangeReason.Moved, key: item1.Id, current: item1, previous: default, currentIndex: 4, previousIndex: 1)
            });


            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(7, "8 source operations were performed, and 1 should have been ignored");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "11 items were added, 2 were replaced, and 1 was refreshed");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(7).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item8, item9, item10, item11 }, "item #1 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(8).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item9, item10, item11 }, "item #8 should have expired");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(9).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #2, #6, and #7 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(10).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(11).Should().BeEmpty("no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(11).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have expired");
            results.RecordedItemsSorted.Should().BeEmpty();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(12).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have expired");
            results.RecordedItemsSorted.Should().BeEmpty();

            // Remaining item should never expire
            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(13).Should().BeEmpty("no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void RemovalsArePending_CompletionWaitsForRemovals()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2 };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3)
            });
            scheduler.AdvanceBy(1);

            // Verify initial state, after all emissions
            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3 }, "item #1 should have been removed");

            source.OnCompleted();

            results.Error.Should().BeNull();
            results.HasCompleted.Should().BeFalse("removals are pending");
            results.RecordedChangeSets.Skip(2).Should().BeEmpty("no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.HasCompleted.Should().BeTrue();
            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2 }, "item #3 should have expired");
        }

        // Covers https://github.com/reactivemarbles/DynamicData/issues/716
        [Fact]
        public void SchedulerIsInaccurate_RemovalsAreNotSkipped()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = new FakeScheduler()
            {
                Now = DateTimeOffset.FromUnixTimeMilliseconds(0)
            };

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });


            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

            scheduler.SimulateUntilIdle(inaccuracyOffset: TimeSpan.FromMilliseconds(-1));

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedItemsByKey.Values.Should().BeEmpty("item #1 should have been removed");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void SourceCompletes_CompletionIsPropagated()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1 };
            var item2 = new TestItem() { Id = 2 };
            var item3 = new TestItem() { Id = 3 };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                new(reason: ChangeReason.Add, key: item3.Id, current: item3)
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            source.OnCompleted();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no changes should have occurred");

            results.HasCompleted.Should().BeTrue();
        }

        [Fact]
        public void SourceCompletesImmediately_CompletionIsPropagated()
        {
            var item1 = new TestItem() { Id = 1 };
            var item2 = new TestItem() { Id = 2 };
            var item3 = new TestItem() { Id = 3 };

            var source = Observable.Create<IChangeSet<TestItem, int>>(observer =>
            {
                observer.OnNext(new ChangeSet<TestItem, int>()
                {
                    new(reason: ChangeReason.Add, key: item1.Id, current: item1),
                    new(reason: ChangeReason.Add, key: item2.Id, current: item2),
                    new(reason: ChangeReason.Add, key: item3.Id, current: item3)
                });

                observer.OnCompleted();

                return Disposable.Empty;
            });

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were added");

            results.HasCompleted.Should().BeTrue();
        }

        [Fact]
        public void SourceErrors_ErrorIsPropagated()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: item => item.Expiration - scheduler.Now,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operations was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

            var error = new Exception("This is a test");
            source.OnError(error);

            results.Error.Should().Be(error);
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no changes should have occurred");
            results.HasCompleted.Should().BeFalse();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.RecordedChangeSets.Skip(1).Should().BeEmpty("notifications should not get published after an error");
        }

        [Fact]
        public void SourceErrorsImmediately_ErrorIsPropagated()
        {
            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };

            var error = new Exception("This is a test");

            var source = Observable.Create<IChangeSet<TestItem, int>>(observer =>
            {
                observer.OnNext(new ChangeSet<TestItem, int>()
                {
                    new(reason: ChangeReason.Add, key: item1.Id, current: item1)
                });

                observer.OnError(error);

                return Disposable.Empty;
            });

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: item => item.Expiration - scheduler.Now,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            results.Error.Should().Be(error);
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operations was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");
            results.HasCompleted.Should().BeFalse();

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.RecordedChangeSets.Skip(1).Should().BeEmpty("notifications should not get published after an error");
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.ExpireAfter(
                source: (null as IObservable<IChangeSet<TestItem, int>>)!,
                timeSelector: static _ => default))
            .Should().Throw<ArgumentNullException>();

        [Fact]
        public async Task ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe()
        {
            using var source = new Subject<IChangeSet<StressItem, int>>();

            var scheduler = ThreadPoolScheduler.Instance;

            using var subscription = source
                .ExpireAfter(
                    timeSelector: static item => item.Lifetime,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            PublishStressChangeSets(
                source: source,
                editCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(2),
                maxItemLifetime: TimeSpan.FromMilliseconds(10),
                maxChangeCount: 20);

            await WaitForCompletionAsync(results, timeout: TimeSpan.FromMinutes(1));

            results.Error.Should().BeNull();
            results.RecordedItemsByKey.Values.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task ThreadPoolSchedulerIsUsedWithPolling_ExpirationIsThreadSafe()
        {
            using var source = new Subject<IChangeSet<StressItem, int>>();

            var scheduler = ThreadPoolScheduler.Instance;

            using var subscription = source
                .ExpireAfter(
                    timeSelector: static item => item.Lifetime,
                    pollingInterval: TimeSpan.FromMilliseconds(10),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            PublishStressChangeSets(
                source: source,
                editCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(2),
                maxItemLifetime: TimeSpan.FromMilliseconds(10),
                maxChangeCount: 20);

            await WaitForCompletionAsync(results, timeout: TimeSpan.FromMinutes(1));

            var now = scheduler.Now;

            results.Error.Should().BeNull();
            results.RecordedItemsByKey.Values.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void TimeSelectorIsNull_ThrowsException()
            => FluentActions.Invoking(() => new Subject<IChangeSet<TestItem, int>>().ExpireAfter(
                timeSelector: null!))
            .Should().Throw<ArgumentNullException>();

        [Fact]
        public void TimeSelectorThrows_ErrorIsPropagated()
        {
            using var source = new Subject<IChangeSet<TestItem, int>>();

            var scheduler = CreateTestScheduler();

            var error = new Exception("This is a test.");

            using var subscription = source
                .ExpireAfter(
                    timeSelector: _ => throw error,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.OnNext(new ChangeSet<TestItem, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });
            scheduler.AdvanceBy(1);

            results.Error.Should().Be(error);
            results.RecordedChangeSets.Should().BeEmpty("no source operations should have been processed");
            results.HasCompleted.Should().BeFalse();
        }

        private static void PublishStressChangeSets(
            IObserver<IChangeSet<StressItem, int>> source,
            int editCount,
            TimeSpan minItemLifetime,
            TimeSpan maxItemLifetime,
            int maxChangeCount)
        {
            // Not exercising Moved, since ChangeAwareCache<> doesn't support it, and I'm too lazy to implement it by hand.
            var changeReasons = new[]
            {
                ChangeReason.Add,
                ChangeReason.Refresh,
                ChangeReason.Remove,
                ChangeReason.Update
            };

            // Weights are chosen to make the cache size likely to grow over time,
            // exerting more pressure on the system the longer the benchmark runs.
            // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
            var changeReasonWeightsWhenCountIs0 = new[]
            {
                1f, // Add
                0f, // Refresh
                0f, // Remove
                0f  // Update
            };

            var changeReasonWeightsOtherwise = new[]
            {
                0.30f, // Add
                0.25f, // Refresh
                0.20f, // Remove
                0.25f  // Update
            };

            var randomizer = new Randomizer(1234567);

            var nextItemId = 1;

            var changeSets = new List<IChangeSet<StressItem, int>>(capacity: editCount);

            var cache = new ChangeAwareCache<StressItem, int>();

            while (changeSets.Count < changeSets.Capacity)
            {
                var changeCount = randomizer.Int(1, maxChangeCount);
                for (var i = 0; i < changeCount; ++i)
                {
                    var changeReason = randomizer.WeightedRandom(changeReasons, cache.Count switch
                    {
                        0   => changeReasonWeightsWhenCountIs0,
                        _   => changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ChangeReason.Add:
                            cache.AddOrUpdate(
                                item:   new StressItem()
                                {
                                    Id          = nextItemId,
                                    Lifetime    = randomizer.Bool()
                                        ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                                        : null
                                },
                                key:    nextItemId);
                            ++nextItemId;
                            break;

                        case ChangeReason.Refresh:
                            cache.Refresh(cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1)));
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1)));
                            break;

                        case ChangeReason.Update:
                            var id = cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1));
                            cache.AddOrUpdate(
                                item:   new StressItem()
                                {
                                    Id          = id,
                                    Lifetime    = randomizer.Bool()
                                        ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                                        : null
                                },
                                key:    id);
                            break;
                    }
                }

                changeSets.Add(cache.CaptureChanges());
            }

            foreach(var changeSet in changeSets)
                source.OnNext(changeSet);
        }
    
        private static async Task WaitForCompletionAsync(
            CacheItemRecordingObserver<StressItem, int> results,
            TimeSpan timeout)
        {
            // Wait up to full minute for the operator to finish processing expirations
            // (this is mainly a problem for GitHub PR builds, where test runs take a lot longer, due to more limited resources).
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var pollingInterval = TimeSpan.FromMilliseconds(100);
            while (stopwatch.Elapsed < timeout)
            {
                await Task.Delay(pollingInterval);

                // Identify "completion" as either an error, a completion signal, or all expiring items being removed.
                if ((results.Error is not null) 
                    || results.HasCompleted
                    || results.RecordedItemsByKey.Values.All(static item => item.Lifetime is null))
                {
                    break;
                }
            }
        }
    }
}
