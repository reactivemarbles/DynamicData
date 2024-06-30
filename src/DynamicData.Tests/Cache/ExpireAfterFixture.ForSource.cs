using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class ExpireAfterFixture
{
    public sealed class ForSource
    {
        private readonly ITestOutputHelper _output;

        public ForSource(ITestOutputHelper output)
            => _output = output;

        [Fact]
        public void ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
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

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no items should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item1, item3, item4 }, "3 items were added, and one was removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues[0].Should().BeEquivalentTo(new[] { item1, item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "items #1 and #3 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item4 }, "items #1 and #3 should have been removed");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
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

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item2 }, "item #1 was added, and then replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item2 }, "no changes should have occurred");

            // Shorten the expiration to an earlier time
            var item3 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item3);
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item3 }, "item #1 was replaced");

            // One more update with no changes to the expiration
            var item4 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
            source.AddOrUpdate(item4);
            scheduler.AdvanceBy(1);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item4 }, "item #1 was replaced");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues[0].Should().BeEquivalentTo(new[] { item4 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item4)), "item #1 should have expired");
            source.Items.Should().BeEmpty("item #1 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(1).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEmpty("no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
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
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.AddOrUpdate(new[] { item6, item7 });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
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
            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "9 items were added, 2 were replaced, and 1 was refreshed");

            // Item scheduled to expire at 10ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "no changes should have occurred");

            // Item scheduled to expire at 15ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues[0].Should().BeEquivalentTo(new[] { item1, item2, item6, item7, item8 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "items #1, #2, #6, #7, and #8 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #1, #2, #6, #7, and #8 should have been removed");

            // Item scheduled to expire at 30ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(1).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "no changes should have occurred");

            // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #3 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");

            // Item scheduled to expire at 45ms, but won't be picked up yet
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(2).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #10 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have been removed");

            // Expired items should be polled, but none should be found
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(3).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "no changes should have occurred");

            // Expired items should be polled
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #11 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have been removed");

            // Next poll should not find anything to expire.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(4).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item9 }, "no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
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
            var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
            source.AddOrUpdate(new[] { item6, item7 });
            scheduler.AdvanceBy(1);

            // Out-of-order expiration
            var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
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
            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "11 items were added, 2 were replaced, and 1 was refreshed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues[0].Should().BeEquivalentTo(new[] { item1 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #1 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item8, item9, item10, item11 }, "item #1 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item8 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #8 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item9, item10, item11 }, "item #8 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item2, item6, item7 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "items #2, #6, and #7 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #2, #6, and #7 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #3 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have been removed");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(4).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "no changes should have occurred");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(4).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #10 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have expired");

            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(5).Count().Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues.Skip(5).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #11 should have expired");
            source.Items.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have expired");

            // Remaining item should never expire
            scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

            results.Error.Should().BeNull();
            results.RecordedValues.Skip(6).Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item9 }, "no changes should have occurred");

            results.HasCompleted.Should().BeFalse();
        }

        // Covers https://github.com/reactivemarbles/DynamicData/issues/716
        [Fact]
        public void SchedulerIsInaccurate_RemovalsAreNotSkipped()
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


            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            source.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

            scheduler.SimulateUntilIdle(inaccuracyOffset: TimeSpan.FromMilliseconds(-1));

            results.Error.Should().BeNull();
            results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
            results.RecordedValues[0].Should().BeEquivalentTo(new[] { item1 }.Select(item => new KeyValuePair<int, TestItem>(item.Id, item)), "item #1 should have expired");
            source.Items.Should().BeEmpty("item #1 should have been removed");

            results.HasCompleted.Should().BeFalse();
        }

        [Fact]
        public void SourceCompletes_CompletionIsPropagated()
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

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            results.HasCompleted.Should().BeTrue();

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Fact]
        public void SourceCompletesImmediately_CompletionIsPropagated()
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

            results.Error.Should().BeNull();
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            results.HasCompleted.Should().BeTrue();
            source.Items.Should().BeEquivalentTo(new[] { item1 }, "no changes should have occurred");
        }

        [Fact]
        public void SourceErrors_ErrorIsPropagated()
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

            results.Error.Should().Be(error, "an error was published");
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            results.HasCompleted.Should().BeFalse();

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Fact]
        public void SourceErrorsImmediately_ErrorIsPropagated()
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

            results.Error.Should().Be(error, "an error was published");
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            results.HasCompleted.Should().BeFalse();
            source.Items.Should().BeEquivalentTo(new[] { item1 }, "no changes should have occurred");

            // Ensure that the operator does not attept to continue removing items.
            scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);
        }

        [Fact]
        public void SourceIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.ExpireAfter(
                    source: (null as ISourceCache<TestItem, int>)!,
                    timeSelector: static _ => default,
                    pollingInterval: null))
                .Should().Throw<ArgumentNullException>();

        [Fact]
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

            results.Error.Should().BeNull();
            results.RecordedValues.SelectMany(static removals => removals).Should().AllSatisfy(static pair => pair.Value.Lifetime.Should().NotBeNull("only items with an expiration should have expired"));
            results.HasCompleted.Should().BeFalse();
            source.Items.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

            _output.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
        }

        [Fact]
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

            results.Error.Should().BeNull();
            results.RecordedValues.SelectMany(static removals => removals).Should().AllSatisfy(pair => pair.Value.Lifetime.Should().NotBeNull("only items with an expiration should have expired"));
            results.HasCompleted.Should().BeFalse();
            source.Items.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

            _output.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
        }

        [Fact]
        public void TimeSelectorIsNull_ThrowsException()
            => FluentActions.Invoking(() => CreateTestSource().ExpireAfter(
                    timeSelector: null!,
                    pollingInterval: null))
                .Should().Throw<ArgumentNullException>();

        [Fact]
        public void TimeSelectorThrows_ErrorIsPropagated()
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

            results.Error.Should().Be(error);
            results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
            results.HasCompleted.Should().BeFalse();
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
                    Id          = id,
                    Lifetime    = randomizer.Bool()
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
                            0   => changeReasonWeightsWhenCountIs0,
                            _   => changeReasonWeightsOtherwise
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
                                    Id          = updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)),
                                    Lifetime    = randomizer.Bool()
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
