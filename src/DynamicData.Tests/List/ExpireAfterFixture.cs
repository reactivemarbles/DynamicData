using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Microsoft.Reactive.Testing;

using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public sealed class ExpireAfterFixture
{
    private readonly ITestOutputHelper _output;

    public ExpireAfterFixture(ITestOutputHelper output)
        => _output = output;

    [Fact]
    public void ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
    {
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordValues(out var results, scheduler);

        var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item5 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.AddRange(new[] { item1, item2, item3, item4, item5, item6 });
        scheduler.AdvanceBy(1);

        var item7 = new TestItem() { Id = 7 };
        source.Add(item7);
        scheduler.AdvanceBy(1);

        source.Remove(item2);
        scheduler.AdvanceBy(1);

        // item4 and item5
        source.RemoveRange(index: 2, count: 2);
        scheduler.AdvanceBy(1);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no items should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item1, item3, item6, item7 }, "7 items were added, and 3 were removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.ElementAt(0).Should().BeEquivalentTo(new[] { item1, item3, item6 }, "items #1, #3, and #6 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item7 }, "items #1 and #3 should have been removed");

        results.HasCompleted.Should().BeFalse();
    }

    [Fact]
    public void NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
    {
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordValues(out var results, scheduler);

        var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
        scheduler.AdvanceBy(1);

        // Extend the expiration to a later time
        var item2 = new TestItem() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
        source.Replace(item1, item2);
        scheduler.AdvanceBy(1);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item2 }, "item #1 was added, and then replaced");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item2 }, "no changes should have occurred");

        // Shorten the expiration to an earlier time
        var item3 = new TestItem() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
        source.Replace(item2, item3);
        scheduler.AdvanceBy(1);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item3 }, "item #2 was replaced");

        // One more update with no changes to the expiration
        var item4 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
        source.Replace(item3, item4);
        scheduler.AdvanceBy(1);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item4 }, "item #3 was replaced");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.ElementAt(0).Should().BeEquivalentTo(new[] { item4 }, "item #4 should have expired");
        source.Items.Should().BeEmpty("item #4 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(1).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEmpty("no changes should have occurred");

        results.HasCompleted.Should().BeFalse();
    }

    [Fact]
    public void PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
    {
        using var source = new TestSourceList<TestItem>();

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
        source.AddRange(new[] { item1, item2, item3, item4, item5 });
        scheduler.AdvanceBy(1);

        // Additional expirations at 20ms.
        var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        source.AddRange(new[] { item6, item7 });
        scheduler.AdvanceBy(1);

        // Out-of-order expiration
        var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
        source.Add(item8);
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item9 = new TestItem() { Id = 9 };
        source.Add(item9);
        scheduler.AdvanceBy(1);

        // Replacement changing lifetime.
        var item10 = new TestItem() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
        source.Replace(item4, item10);
        scheduler.AdvanceBy(1);

        // Replacement not-affecting lifetime.
        var item11 = new TestItem() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
        source.Replace(item5, item11);
        scheduler.AdvanceBy(1);

        // Move should not affect scheduled expiration.
        item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
        source.Move(2, 3);
        scheduler.AdvanceBy(1);

        // Not testing Refresh changes, since ISourceList<T> doesn't actually provide an API to generate them.


        // Verify initial state, after all emissions
        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "9 items were added, 2 were replaced, and 1 was refreshed");

        // Item scheduled to expire at 10ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Item scheduled to expire at 15ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.ElementAt(0).Should().BeEquivalentTo(new[] { item1, item2, item6, item7, item8 }, "items #1, #2, #6, #7, and #8 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "items #1, #2, #6, #7, and #8 should have been removed");

        // Item scheduled to expire at 30ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(1).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }, "item #3 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "item #3 should have been removed");

        // Item scheduled to expire at 45ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(2).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }, "item #10 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, "item #10 should have been removed");

        // Expired items should be polled, but none should be found
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(3).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }, "item #11 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "item #11 should have been removed");

        // Next poll should not find anything to expire.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(4).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        results.HasCompleted.Should().BeFalse();
    }

    [Fact]
    public void PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
    {
        using var source = new TestSourceList<TestItem>();

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
        source.AddRange(new[] { item1, item2, item3, item4, item5 });
        scheduler.AdvanceBy(1);

        // Additional expirations at 20ms.
        var item6 = new TestItem() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        var item7 = new TestItem() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        source.AddRange(new[] { item6, item7 });
        scheduler.AdvanceBy(1);

        // Out-of-order expiration
        var item8 = new TestItem() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
        source.Add(item8);
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item9 = new TestItem() { Id = 9 };
        source.Add(item9);
        scheduler.AdvanceBy(1);

        // Replacement changing lifetime.
        var item10 = new TestItem() { Id = 10, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
        source.Replace(item4, item10);
        scheduler.AdvanceBy(1);

        // Replacement not-affecting lifetime.
        var item11 = new TestItem() { Id = 11, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
        source.Replace(item5, item11);
        scheduler.AdvanceBy(1);

        // Moved items should still expire correctly, but its expiration time should not change.
        item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
        source.Move(2, 3);
        scheduler.AdvanceBy(1);


        // Verify initial state, after all emissions
        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "9 items were added, 2 were replaced, and 1 was moved");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.ElementAt(0).Should().BeEquivalentTo(new[] { item1 }, "item #1 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "item #1 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item8 }, "item #8 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item2, item10, item3, item11, item6, item7, item9 }, options => options.WithStrictOrdering(), "item #8 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item2, item6, item7 }, "items #2, #6, and #7 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "items #2, #6, and #7 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }, "item #3 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "item #3 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(4).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(4).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }, "item #10 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, options => options.WithStrictOrdering(), "item #10 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(5).Count().Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.Skip(5).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }, "item #11 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "item #11 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

        results.Error.Should().BeNull();
        results.RecordedValues.Skip(6).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        results.HasCompleted.Should().BeFalse();
    }

    // Covers https://github.com/reactivemarbles/DynamicData/issues/716
    [Fact]
    public void SchedulerIsInaccurate_RemovalsAreNotSkipped()
    {
        using var source = new TestSourceList<TestItem>();

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
        source.Add(item1);


        results.Error.Should().BeNull();
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

        scheduler.SimulateUntilIdle(inaccuracyOffset: TimeSpan.FromMilliseconds(-1));

        results.Error.Should().BeNull();
        results.RecordedValues.Count.Should().Be(1, "1 expiration should have occurred");
        results.RecordedValues.ElementAt(0).Should().BeEquivalentTo(new[] { item1 }, "item #1 should have expired");
        source.Items.Should().BeEmpty("item #1 should have been removed");

        results.HasCompleted.Should().BeFalse();
    }

    [Fact]
    public void SourceCompletes_CompletionIsPropagated()
    {
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordValues(out var results, scheduler);

        source.Add(new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
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
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
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
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordValues(out var results, scheduler);

        source.Add(new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
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
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        var item1 = new TestItem() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
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
        => FluentActions.Invoking(() => ObservableListEx.ExpireAfter(
                source: (null as ISourceList<TestItem>)!,
                timeSelector: static _ => default,
                pollingInterval: null))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public async Task ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe()
    {
        using var source = new TestSourceList<StressItem>();

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
            maxChangeCount: 10,
            maxRangeSize: 50);

        await WaitForCompletionAsync(source, results, TimeSpan.FromMinutes(1));

        results.Error.Should().BeNull();
        results.RecordedValues.SelectMany(static removals => removals).Should().AllSatisfy(static item => item.Lifetime.Should().NotBeNull("only items with an expiration should have expired"));
        results.HasCompleted.Should().BeFalse();
        source.Items.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

        _output.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
    }

    [Fact]
    public async Task ThreadPoolSchedulerIsUsedWithPolling_ExpirationIsThreadSafe()
    {
        using var source = new TestSourceList<StressItem>();

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
            maxChangeCount: 10,
            maxRangeSize: 50);

        await WaitForCompletionAsync(source, results, TimeSpan.FromMinutes(1));

        results.Error.Should().BeNull();
        results.RecordedValues.SelectMany(static removals => removals).Should().AllSatisfy(item => item.Lifetime.Should().NotBeNull("only items with an expiration should have expired"));
        results.HasCompleted.Should().BeFalse();
        source.Items.Should().AllSatisfy(item => item.Lifetime.Should().BeNull("all items with an expiration should have expired"));

        _output.WriteLine($"{results.RecordedValues.Count} Expirations occurred, for {results.RecordedValues.SelectMany(static item => item).Count()} items");
    }

    [Fact]
    public void TimeSelectorIsNull_ThrowsException()
        => FluentActions.Invoking(() => new TestSourceList<TestItem>().ExpireAfter(
                timeSelector: null!,
                pollingInterval: null))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void TimeSelectorThrows_ThrowsException()
    {
        using var source = new TestSourceList<TestItem>();

        var scheduler = CreateTestScheduler();

        var error = new Exception("This is a test.");

        using var subscription = source
            .ExpireAfter(
                timeSelector: _ => throw error,
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordValues(out var results, scheduler);

        source.Add(new TestItem() { Id = 1 });
        scheduler.AdvanceBy(1);

        results.Error.Should().Be(error);
        results.RecordedValues.Should().BeEmpty("no expirations should have occurred");
        results.HasCompleted.Should().BeFalse();
    }

    private static TestScheduler CreateTestScheduler()
    {
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks);

        return scheduler;
    }

    private static Func<TestItem, TimeSpan?> CreateTimeSelector(IScheduler scheduler)
        => item => item.Expiration - scheduler.Now;

    private static void PerformStressEdits(
        ISourceList<StressItem> source,
        int editCount,
        TimeSpan minItemLifetime,
        TimeSpan maxItemLifetime,
        int maxChangeCount,
        int maxRangeSize)
    {
        // Not exercising Refresh, since SourceList<> doesn't support it.
        var changeReasons = new[]
        {
            ListChangeReason.Add,
            ListChangeReason.AddRange,
            ListChangeReason.Clear,
            ListChangeReason.Moved,
            ListChangeReason.Remove,
            ListChangeReason.RemoveRange,
            ListChangeReason.Replace
        };

        // Weights are chosen to make the list size likely to grow over time,
        // exerting more pressure on the system the longer the benchmark runs,
        // while still ensuring that at least a few clears are executed.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty list).
        var changeReasonWeightsWhenCountIs0 = new[]
        {
            0.5f,   // Add
            0.5f,   // AddRange
            0.0f,   // Clear
            0.0f,   // Moved
            0.0f,   // Remove
            0.0f,   // RemoveRange
            0.0f    // Replace
        };

        var changeReasonWeightsWhenCountIs1 = new[]
        {
            0.250f, // Add
            0.250f, // AddRange
            0.001f, // Clear
            0.000f, // Moved
            0.150f, // Remove
            0.150f, // RemoveRange
            0.199f  // Replace
        };

        var changeReasonWeightsOtherwise = new[]
        {
            0.200f, // Add
            0.200f, // AddRange
            0.001f, // Clear
            0.149f, // Moved
            0.150f, // Remove
            0.150f, // RemoveRange
            0.150f  // Replace
        };

        var randomizer = new Randomizer(1234567);

        var items = Enumerable.Range(1, editCount * maxChangeCount * maxRangeSize)
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
                        1   => changeReasonWeightsWhenCountIs1,
                        _   => changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ListChangeReason.Add:
                            updater.Add(items[nextItemIndex++]);
                            break;

                        case ListChangeReason.AddRange:
                            updater.AddRange(Enumerable
                                .Range(0, randomizer.Int(1, maxRangeSize))
                                .Select(_ => items[nextItemIndex++]));
                            break;

                        case ListChangeReason.Replace:
                            updater.Replace(
                                original: randomizer.ListItem(updater),
                                replaceWith: items[nextItemIndex++]);
                            break;

                        case ListChangeReason.Remove:
                            updater.RemoveAt(randomizer.Int(0, updater.Count - 1));
                            break;

                        case ListChangeReason.RemoveRange:
                            var removeCount = randomizer.Int(1, Math.Min(maxRangeSize, updater.Count));
                            updater.RemoveRange(
                                index: randomizer.Int(0, updater.Count - removeCount),
                                count: removeCount);
                            break;

                        case ListChangeReason.Moved:
                            int originalIndex;
                            int destinationIndex;

                            do
                            {
                                originalIndex = randomizer.Int(0, updater.Count - 1);
                                destinationIndex = randomizer.Int(0, updater.Count - 1);
                            } while (originalIndex == destinationIndex);

                            updater.Move(originalIndex, destinationIndex);
                            break;

                        case ListChangeReason.Clear:
                            updater.Clear();
                            break;
                    }
                }
            });
        }
    }

    private static async Task WaitForCompletionAsync(
        ISourceList<StressItem> source,
        ValueRecordingObserver<IEnumerable<StressItem>> results,
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

    private sealed class TestItem
    {
        public required int Id { get; init; }

        public DateTimeOffset? Expiration { get; set; }
    }

    private sealed record StressItem
    {
        public required int Id { get; init; }

        public required TimeSpan? Lifetime { get; init; }
    }
}
