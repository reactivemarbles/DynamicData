using System.Diagnostics;

using Bogus;

namespace DynamicData.Tests.Cache;

public static partial class ExpireAfterFixture
{
    public sealed class ForSource
    {
        [Test]
        public async Task ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.AddOrUpdate(new[] { item1, item2, item3 });
            scheduler.AdvanceBy(1);

            var item4 = new TestItem() { Id = 4 };
            source.AddOrUpdate(item4);
            scheduler.AdvanceBy(1);

            source.RemoveKey(2);
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no items should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1, item3, item4 }).Because("3 items were added, and one was removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues[0]).IsEquivalentTo(new[] { item1, item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("items #1 and #3 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item4 }).Because("items #1 and #3 should have been removed");

            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.AddOrUpdate(item1);
            scheduler.AdvanceBy(1);

            // Extend the expiration to a later time
            var item2 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.AddOrUpdate(item2);
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item2 }).Because("item #1 was added, and then replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item2 }).Because("no changes should have occurred");

            // Shorten the expiration to an earlier time
            var item3 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item3);
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item3 }).Because("item #1 was replaced");

            // One more update with no changes to the expiration
            var item4 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item4);
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item4 }).Because("item #1 was replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues[0]).IsEquivalentTo(new[] { item4 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item4))).Because("item #1 should have expired");
            await Assert.That(source.Items).IsEmpty().Because("item #1 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(1)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEmpty().Because("no changes should have occurred");

            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    pollingInterval: TimeSpan.FromMilliseconds(20),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.AddOrUpdate(new[] { item1, item2, item3, item4, item5 });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.AddOrUpdate(new[] { item6, item7 });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item8);
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new TestItem() { Id = 9 };
            source.AddOrUpdate(item9);
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.AddOrUpdate(item10);
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
            source.AddOrUpdate(item11);
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.Refresh(item3);
            scheduler.AdvanceBy(1);

            // Not testing Move changes, since ISourceCache<T> doesn't actually provide an API to generate them.

            // Verify initial state, after all emissions
            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }).Because("9 items were added, 2 were replaced, and 1 was refreshed");

            // Item scheduled to expire at 10ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }).Because("no changes should have occurred");

            // Item scheduled to expire at 15ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }).Because("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues[0]).IsEquivalentTo(new[] { item1, item2, item6, item7, item8 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("items #1, #2, #6, #7, and #8 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item3, item9, item10, item11 }).Because("items #1, #2, #6, #7, and #8 should have been removed");

            // Item scheduled to expire at 30ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(1)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item3, item9, item10, item11 }).Because("no changes should have occurred");

            // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(1).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(1).ElementAt(0)).IsEquivalentTo(new[] { item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #3 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item10, item11 }).Because("item #3 should have been removed");

            // Item scheduled to expire at 45ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(2)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item10, item11 }).Because("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(2).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(2).ElementAt(0)).IsEquivalentTo(new[] { item10 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #10 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item11 }).Because("item #10 should have been removed");

            // Expired items should be polled, but none should be found
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(3)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item11 }).Because("no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(3).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(3).ElementAt(0)).IsEquivalentTo(new[] { item11 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #11 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9 }).Because("item #11 should have been removed");

            // Next poll should not find anything to expire.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(4)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9 }).Because("no changes should have occurred");

            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
            var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
            var item5 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.AddOrUpdate(new[] { item1, item2, item3, item4, item5 });
            scheduler.AdvanceBy(1);

            // Additional expirations at 20ms.
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
            source.AddOrUpdate(new[] { item6, item7 });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item8);
            scheduler.AdvanceBy(1);

            // Non-expiring item
            var item9 = new TestItem() { Id = 9 };
            source.AddOrUpdate(item9);
            scheduler.AdvanceBy(1);

            // Replacement changing lifetime.
            var item10 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
            source.AddOrUpdate(item10);
            scheduler.AdvanceBy(1);

            // Replacement not-affecting lifetime.
            var item11 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
            source.AddOrUpdate(item11);
            scheduler.AdvanceBy(1);

            // Refresh should not affect scheduled expiration.
            item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
            source.Refresh(item3);
            scheduler.AdvanceBy(1);

            // Not testing Move changes, since ISourceCache<T> doesn't actually provide an API to generate them.

            // Verify initial state, after all emissions
            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }).Because("11 items were added, 2 were replaced, and 1 was refreshed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues[0]).IsEquivalentTo(new[] { item1 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #1 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item2, item3, item6, item7, item8, item9, item10, item11 }).Because("item #1 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(1).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(1).ElementAt(0)).IsEquivalentTo(new[] { item8 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #8 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item2, item3, item6, item7, item9, item10, item11 }).Because("item #8 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(2).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(2).ElementAt(0)).IsEquivalentTo(new[] { item2, item6, item7 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("items #2, #6, and #7 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item3, item9, item10, item11 }).Because("items #2, #6, and #7 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(3).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(3).ElementAt(0)).IsEquivalentTo(new[] { item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #3 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item10, item11 }).Because("item #3 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(4)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item10, item11 }).Because("no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(4).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(4).ElementAt(0)).IsEquivalentTo(new[] { item10 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #10 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9, item11 }).Because("item #10 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(5).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues.Skip(5).ElementAt(0)).IsEquivalentTo(new[] { item11 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #11 should have expired");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9 }).Because("item #11 should have expired");

            // Remaining item should never expire
            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Skip(6)).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item9 }).Because("no changes should have occurred");

            await Assert.That(results.HasCompleted).IsFalse();
        }

        // Covers https://github.com/reactivemarbles/DynamicData/issues/716
        [Test]
        public async Task SchedulerIsInaccurate_RemovalsAreNotSkipped()
        {
            using var source = CreateTestSource();

            var scheduler = new FakeScheduler()
            {
                Now = DateTimeOffset.FromUnixTimeMilliseconds(0)
            };

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.AddOrUpdate(item1);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1 }).Because("1 item was added");

            scheduler.SimulateUntilIdle(inaccuracyOffset: TimeSpan.FromMilliseconds(-1));

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("1 expiration should have occurred");
            await Assert.That(results.RecordedValues[0]).IsEquivalentTo(new[] { item1 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item))).Because("item #1 should have expired");
            await Assert.That(source.Items).IsEmpty().Because("item #1 should have been removed");

            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task SourceCompletes_CompletionIsPropagated()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            source.AddOrUpdate(new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
            scheduler.AdvanceBy(1);

            source.Complete();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(results.HasCompleted).IsTrue();

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Test]
        public async Task SourceCompletesImmediately_CompletionIsPropagated()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.AddOrUpdate(item1);
            scheduler.AdvanceBy(1);

            source.Complete();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1 }).Because("no changes should have occurred");
        }

        [Test]
        public async Task SourceErrors_ErrorIsPropagated()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            source.AddOrUpdate(new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
            scheduler.AdvanceBy(1);

            var error = new Exception("This is a test");
            source.SetError(error);

            await Assert.That(results.Error).IsEqualTo(error).Because("an error was published");
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(results.HasCompleted).IsFalse();

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Test]
        public async Task SourceErrorsImmediately_ErrorIsPropagated()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
            source.AddOrUpdate(item1);
            scheduler.AdvanceBy(1);

            var error = new Exception("This is a test");
            source.SetError(error);

            using var subscription = source
                .ExpireAfter(
                    timeSelector: CreateTimeSelector(scheduler),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            await Assert.That(results.Error).IsEqualTo(error).Because("an error was published");
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(source.Items).IsEquivalentTo(new[] { item1 }).Because("no changes should have occurred");

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(() => ObservableCacheEx.ExpireAfter(
                    source: (null as ISourceCache<TestItem, int>)!,
                    timeSelector: static _ => default,
                    pollingInterval: null)).Throws<ArgumentNullException>();

        [Test]
        public async Task ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe()
        {
            using var source = new TestSourceCache<StressItem, int>(static item => item.Id);

            var scheduler = ThreadPoolScheduler.Instance;

            using var subscription = source
                .ExpireAfter(
                    timeSelector: static item => item.Lifetime,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            PerformStressEdits(
                source: source,
                editCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(2),
                maxItemLifetime: TimeSpan.FromMilliseconds(10),
                maxChangeCount: 10);

            await WaitForCompletionAsync(source, results, timeout: TimeSpan.FromMinutes(1));

            await Assert.That(results.Error).IsNull();
            foreach (var pair in results.RecordedValues.SelectMany(static removals => removals)) { await Assert.That(pair.Value.Lifetime).IsNotNull().Because("only items with an expiration should have expired"); }
            await Assert.That(results.HasCompleted).IsFalse();
            foreach (var item in source.Items) { await Assert.That(item.Lifetime).IsNull().Because("all items with an expiration should have expired"); }

            TestContext.Current?.OutputWriter.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
        }

        [Test]
        public async Task ThreadPoolSchedulerIsUsedWithPolling_ExpirationIsThreadSafe()
        {
            using var source = new TestSourceCache<StressItem, int>(static item => item.Id);

            var scheduler = ThreadPoolScheduler.Instance;

            using var subscription = source
                .ExpireAfter(
                    timeSelector: static item => item.Lifetime,
                    pollingInterval: TimeSpan.FromMilliseconds(10),
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            PerformStressEdits(
                source: source,
                editCount: 10_000,
                minItemLifetime: TimeSpan.FromMilliseconds(2),
                maxItemLifetime: TimeSpan.FromMilliseconds(10),
                maxChangeCount: 10);

            await WaitForCompletionAsync(source, results, timeout: TimeSpan.FromMinutes(1));

            await Assert.That(results.Error).IsNull();
            foreach (var pair in results.RecordedValues.SelectMany(static removals => removals)) { await Assert.That(pair.Value.Lifetime).IsNotNull().Because("only items with an expiration should have expired"); }
            await Assert.That(results.HasCompleted).IsFalse();
            foreach (var item in source.Items) { await Assert.That(item.Lifetime).IsNull().Because("all items with an expiration should have expired"); }

            TestContext.Current?.OutputWriter.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
        }

        [Test]
        public async Task TimeSelectorIsNull_ThrowsException()
            => await Assert.That(() => CreateTestSource().ExpireAfter(
                    timeSelector: null!,
                    pollingInterval: null)).Throws<ArgumentNullException>();

        [Test]
        public async Task TimeSelectorThrows_ErrorIsPropagated()
        {
            using var source = CreateTestSource();

            var scheduler = CreateTestScheduler();

            var error = new Exception("This is a test.");

            using var subscription = source
                .ExpireAfter(
                    timeSelector: _ => throw error,
                    scheduler: scheduler)
                .ValidateSynchronization()
                .RecordValues(out var results, scheduler);

            source.AddOrUpdate(new TestItem() { Id = 1 });
            scheduler.AdvanceBy(1);

            await Assert.That(results.Error).IsEqualTo(error);
            await Assert.That(results.RecordedValues).IsEmpty().Because("no expirations should have occurred");
            await Assert.That(results.HasCompleted).IsFalse();
        }

        private static TestSourceCache<TestItem, int> CreateTestSource()
            => new(static item => item.Id);

        private static void PerformStressEdits(
            ISourceCache<StressItem, int> source,
            int editCount,
            TimeSpan minItemLifetime,
            TimeSpan maxItemLifetime,
            int maxChangeCount)
        {
            // Not exercising Moved, since SourceCache<> doesn't support it.
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

            var items = Enumerable.Range(1, editCount * maxChangeCount)
                .Select(id => new StressItem()
                {
                    Id = id,
                    Lifetime = randomizer.Bool()
                        ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                        : null
                })
                .ToArray();

            var nextItemIndex = 0;

            for (var i = 0; i < editCount; ++i)
            {
                source.Edit(updater =>
                {
                    var changeCount = randomizer.Int(1, maxChangeCount);
                    for (var i = 0; i < changeCount; ++i)
                    {
                        var changeReason = randomizer.WeightedRandom(changeReasons, updater.Count switch
                        {
                            0 => changeReasonWeightsWhenCountIs0,
                            _ => changeReasonWeightsOtherwise
                        });

                        switch (changeReason)
                        {
                            case ChangeReason.Add:
                                updater.AddOrUpdate(items[nextItemIndex++]);
                                break;

                            case ChangeReason.Refresh:
                                updater.Refresh(updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)));
                                break;

                            case ChangeReason.Remove:
                                updater.RemoveKey(updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)));
                                break;

                            case ChangeReason.Update:
                                updater.AddOrUpdate(new StressItem()
                                {
                                    Id = updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)),
                                    Lifetime = randomizer.Bool()
                                        ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                                        : null
                                });
                                break;
                        }
                    }
                });
            }
        }

        private static async Task WaitForCompletionAsync(
            ISourceCache<StressItem, int> source,
            ValueRecordingObserver<IEnumerable<KeyValuePair<int, StressItem>>> results,
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

                // Identify "completion" as either the stream finalizing, or there being no remaining items that need to expire
                if (results.HasFinalized || source.Items.All(static item => item.Lifetime is null))
                    break;
            }
        }
    }
}
