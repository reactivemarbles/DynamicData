using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Microsoft.Reactive.Testing;

using Bogus;
using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;
using System.Collections.Generic;

namespace DynamicData.Tests.List;

public sealed class ExpireAfterFixture
{
    [Fact]
    public void ItemIsRemovedBeforeExpiration_ExpirationIsCancelled()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.AddRange(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        var item4 = new Item() { Id = 4 };
        source.Add(item4);
        scheduler.AdvanceBy(1);

        source.Remove(item2);
        scheduler.AdvanceBy(1);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no items should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item1, item3, item4 }, "3 items were added, and one was removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().ElementAt(0).Should().BeEquivalentTo(new[] { item1, item3 }, "items #1 and #3 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item4 }, "items #1 and #3 should have been removed");

        results.TryGetRecordedCompletion().Should().BeFalse();
    }

    [Fact]
    public void NextItemToExpireIsReplaced_ExpirationIsRescheduledIfNeeded()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
        scheduler.AdvanceBy(1);

        // Extend the expiration to a later time
        var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
        source.Replace(item1, item2);
        scheduler.AdvanceBy(1);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item2 }, "item #1 was added, and then replaced");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item2 }, "no changes should have occurred");

        // Shorten the expiration to an earlier time
        var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
        source.Replace(item2, item3);
        scheduler.AdvanceBy(1);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item3 }, "item #2 was replaced");

        // One more update with no changes to the expiration
        var item4 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15) };
        source.Replace(item3, item4);
        scheduler.AdvanceBy(1);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item4 }, "item #3 was replaced");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().ElementAt(0).Should().BeEquivalentTo(new[] { item4 }, "item #4 should have expired");
        source.Items.Should().BeEmpty("item #4 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(1).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEmpty("no changes should have occurred");

        results.TryGetRecordedCompletion().Should().BeFalse();
    }

    [Fact(Skip = "Existing defect, operator emits empty sets of expired items, instead of skipping emission")]
    public void PollingIntervalIsGiven_RemovalsAreScheduledAtInterval()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                pollingInterval: TimeSpan.FromMilliseconds(20),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
        var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
        var item4 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
        var item5 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
        source.AddRange(new[] { item1, item2, item3, item4, item5 });
        scheduler.AdvanceBy(1);

        // Additional expirations at 20ms.
        var item6 = new Item() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        var item7 = new Item() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        source.AddRange(new[] { item6, item7 });
        scheduler.AdvanceBy(1);

        // Out-of-order expiration
        var item8 = new Item() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
        source.Add(item8);
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item9 = new Item() { Id = 9 };
        source.Add(item9);
        scheduler.AdvanceBy(1);

        // Replacement changing lifetime.
        var item10 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
        source.Replace(item4, item10);
        scheduler.AdvanceBy(1);

        // Replacement not-affecting lifetime.
        var item11 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(100) };
        source.Replace(item5, item11);
        scheduler.AdvanceBy(1);

        // Move should not affect scheduled expiration.
        item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
        source.Move(2, 3);
        scheduler.AdvanceBy(1);


        // Verify initial state, after all emissions
        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "9 items were added, 2 were replaced, and 1 was refreshed");

        // Item scheduled to expire at 10ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Item scheduled to expire at 15ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().ElementAt(0).Should().BeEquivalentTo(new[] { item1, item2, item6, item7, item8 }, "items #1, #2, #6, #7, and #8 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "items #1, #2, #6, #7, and #8 should have been removed");

        // Item scheduled to expire at 30ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(1).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled, but should exclude the one that was changed from 40ms to 45ms.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }, "item #3 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "item #3 should have been removed");

        // Item scheduled to expire at 45ms, but won't be picked up yet
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(2).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(60).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }, "item #10 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, "item #10 should have been removed");

        // Expired items should be polled, but none should be found
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(80).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(3).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        // Expired items should be polled
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(100).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }, "item #11 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "item #11 should have been removed");

        // Next poll should not find anything to expire.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(120).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(4).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        results.TryGetRecordedCompletion().Should().BeFalse();
    }

    [Fact(Skip = "Existing defect, very minor defect, items defined to never expire actually do, at DateTimeOffset.MaxValue")]
    public void PollingIntervalIsNotGiven_RemovalsAreScheduledImmediately()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        var item2 = new Item() { Id = 2, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20) };
        var item3 = new Item() { Id = 3, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(30) };
        var item4 = new Item() { Id = 4, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(40) };
        var item5 = new Item() { Id = 5, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
        source.AddRange(new[] { item1, item2, item3, item4, item5 });
        scheduler.AdvanceBy(1);

        // Additional expirations at 20ms.
        var item6 = new Item() { Id = 6, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        var item7 = new Item() { Id = 7, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(20)};
        source.AddRange(new[] { item6, item7 });
        scheduler.AdvanceBy(1);

        // Out-of-order expiration
        var item8 = new Item() { Id = 8, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(15)};
        source.Add(item8);
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item9 = new Item() { Id = 9 };
        source.Add(item9);
        scheduler.AdvanceBy(1);

        // Replacement changing lifetime.
        var item10 = new Item() { Id = 10, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(45) };
        source.Replace(item4, item10);
        scheduler.AdvanceBy(1);

        // Replacement not-affecting lifetime.
        var item11 = new Item() { Id = 11, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(50) };
        source.Replace(item5, item11);
        scheduler.AdvanceBy(1);

        // Move should not affect scheduled expiration.
        item3.Expiration = DateTimeOffset.FromUnixTimeMilliseconds(55);
        source.Move(2, 3);
        scheduler.AdvanceBy(1);


        // Verify initial state, after all emissions
        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1, item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "9 items were added, 2 were replaced, and 1 was moved");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().ElementAt(0).Should().BeEquivalentTo(new[] { item1 }, "item #1 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item2, item10, item3, item11, item6, item7, item8, item9 }, options => options.WithStrictOrdering(), "item #1 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(15).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(1).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(1).ElementAt(0).Should().BeEquivalentTo(new[] { item8 }, "item #8 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item2, item10, item3, item11, item6, item7, item9 }, options => options.WithStrictOrdering(), "item #8 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(20).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(2).ElementAt(0).Should().BeEquivalentTo(new[] { item2, item6, item7 }, "items #2, #6, and #7 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item3, item11, item9 }, options => options.WithStrictOrdering(), "items #2, #6, and #7 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(30).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(3).ElementAt(0).Should().BeEquivalentTo(new[] { item3 }, "item #3 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "item #3 should have been removed");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(40).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(4).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item10, item11, item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(45).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(4).ElementAt(0).Should().BeEquivalentTo(new[] { item10 }, "item #10 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item11, item9 }, options => options.WithStrictOrdering(), "item #10 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(50).Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(5).Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().Skip(5).ElementAt(0).Should().BeEquivalentTo(new[] { item11 }, "item #11 should have expired");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "item #11 should have expired");

        scheduler.AdvanceTo(DateTimeOffset.MaxValue.Ticks);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Skip(6).Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item9 }, options => options.WithStrictOrdering(), "no changes should have occurred");

        results.TryGetRecordedCompletion().Should().BeFalse();
    }

    // Covers https://github.com/reactivemarbles/DynamicData/issues/716
    [Fact(Skip = "Existing defect, removals are skipped when scheduler invokes early")]
    public void SchedulerIsInaccurate_RemovalsAreNotSkipped()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = new FakeScheduler()
        {
            Now = DateTimeOffset.FromUnixTimeMilliseconds(0)
        };

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);


        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        source.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

        // Simulate the scheduler invoking all actions 1ms early.
        while(scheduler.ScheduledActions.Count is not 0)
        {
            if (scheduler.ScheduledActions[0].DueTime is DateTimeOffset dueTime)
                scheduler.Now = dueTime - TimeSpan.FromMilliseconds(1);

            scheduler.ScheduledActions[0].Invoke();
            scheduler.ScheduledActions.RemoveAt(0);
        }

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Count().Should().Be(1, "1 expiration should have occurred");
        results.EnumerateRecordedValues().ElementAt(0).Should().BeEquivalentTo(new[] { item1 }, "item #1 should have expired");
        source.Items.Should().BeEmpty("item #1 should have been removed");

        results.TryGetRecordedCompletion().Should().BeFalse();
    }

    [Fact(Skip = "Existing defect, completion is not propagated from the source")]
    public void SourceCompletes_CompletionIsPropagated()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        source.Add(new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
        scheduler.AdvanceBy(1);

        source.Complete();

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        results.TryGetRecordedCompletion().Should().BeTrue();

        // Ensure that the operator does not attept to continue removing items.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.EnumerateInvalidNotifications().Should().BeEmpty();
    }

    [Fact(Skip = "Existing defect, completion is not propagated from the source")]
    public void SourceCompletesImmediately_CompletionIsPropagated()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
        scheduler.AdvanceBy(1);

        source.Complete();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        results.TryGetRecordedCompletion().Should().BeTrue();
        source.Items.Should().BeEquivalentTo(new[] { item1 }, "no changes should have occurred");

        results.EnumerateInvalidNotifications().Should().BeEmpty();
    }

    [Fact(Skip = "Exsiting defect, errors are re-thrown instead of propagated, operator does not use safe subscriptions")]
    public void SourceErrors_ErrorIsPropagated()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        source.Add(new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) });
        scheduler.AdvanceBy(1);

        var error = new Exception("This is a test");
        source.SetError(error);

        results.TryGetRecordedError().Should().Be(error, "an error was published");
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        results.TryGetRecordedCompletion().Should().BeFalse();

        // Ensure that the operator does not attept to continue removing items.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.EnumerateInvalidNotifications().Should().BeEmpty();
    }

    [Fact(Skip = "Existing defect, immediately-occuring error is not propagated")]
    public void SourceErrorsImmediately_ErrorIsPropagated()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        var item1 = new Item() { Id = 1, Expiration = DateTimeOffset.FromUnixTimeMilliseconds(10) };
        source.Add(item1);
        scheduler.AdvanceBy(1);

        var error = new Exception("This is a test");
        source.SetError(error);

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        results.TryGetRecordedError().Should().Be(error, "an error was published");
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        results.TryGetRecordedCompletion().Should().BeFalse();
        source.Items.Should().BeEquivalentTo(new[] { item1 }, "no changes should have occurred");

        // Ensure that the operator does not attept to continue removing items.
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(10).Ticks);

        results.EnumerateInvalidNotifications().Should().BeEmpty();
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableListEx.ExpireAfter(
                source: (null as ISourceList<Item>)!,
                timeSelector: static _ => default,
                pollingInterval: null))
            .Should().Throw<ArgumentNullException>();

    [Fact(Skip = "Existing defect, operator does not properly handle items with a null timeout, when using a real scheduler, it passes a TimeSpan to the scheduler that is outside of the supported range")]
    public async Task ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = ThreadPoolScheduler.Instance;

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var maxExpiration = PerformStressEdits(
            source: source,
            scheduler: scheduler,
            stressCount: 10_000,
            minItemLifetime: TimeSpan.FromMilliseconds(10),
            maxItemLifetime: TimeSpan.FromMilliseconds(50),
            maxChangeCount: 10,
            maxRangeSize: 10);

        await Observable.Timer(maxExpiration + TimeSpan.FromMilliseconds(100), scheduler);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().SelectMany(static removals => removals).Should().AllSatisfy(static item => item.Expiration.Should().NotBeNull("only items with an expiration should have expired"));
        results.TryGetRecordedCompletion().Should().BeFalse();
        source.Items.Should().AllSatisfy(item => item.Expiration.Should().BeNull("all items with an expiration should have expired"));
    }

    [Fact(Skip = "Existing defect, deadlocks")]
    public async Task ThreadPoolSchedulerIsUsedWithPolling_ExpirationIsThreadSafe()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = ThreadPoolScheduler.Instance;

        using var subscription = source
            .ExpireAfter(
                timeSelector: CreateTimeSelector(scheduler),
                pollingInterval: TimeSpan.FromMilliseconds(10),
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        var maxExpiration = PerformStressEdits(
            source: source,
            scheduler: scheduler,
            stressCount: 10_000,
            minItemLifetime: TimeSpan.FromMilliseconds(10),
            maxItemLifetime: TimeSpan.FromMilliseconds(50),
            maxChangeCount: 10,
            maxRangeSize: 10);

        await Observable.Timer(maxExpiration + TimeSpan.FromMilliseconds(100), scheduler);

        results.TryGetRecordedError().Should().BeNull();
        results.EnumerateRecordedValues().SelectMany(static removals => removals).Should().AllSatisfy(item => item.Expiration.Should().NotBeNull("only items with an expiration should have expired"));
        results.TryGetRecordedCompletion().Should().BeFalse();
        source.Items.Should().AllSatisfy(item => item.Expiration.Should().BeNull("all items with an expiration should have expired"));
    }

    [Fact]
    public void TimeSelectorIsNull_ThrowsException()
        => FluentActions.Invoking(() => new TestSourceList<Item>().ExpireAfter(
                timeSelector: null!,
                pollingInterval: null))
            .Should().Throw<ArgumentNullException>();

    [Fact(Skip = "Exsiting defect, errors are re-thrown instead of propagated, user code is not protected")]
    public void TimeSelectorThrows_ThrowsException()
    {
        using var source = new TestSourceList<Item>();

        var scheduler = CreateTestScheduler();

        var error = new Exception("This is a test.");

        using var subscription = source
            .ExpireAfter(
                timeSelector: _ => throw error,
                scheduler: scheduler)
            .ValidateSynchronization()
            .RecordNotifications(out var results, scheduler);

        source.Add(new Item() { Id = 1 });
        scheduler.AdvanceBy(1);

        results.TryGetRecordedError().Should().Be(error);
        results.EnumerateRecordedValues().Should().BeEmpty("no expirations should have occurred");
        results.TryGetRecordedCompletion().Should().BeFalse();

        results.EnumerateInvalidNotifications().Should().BeEmpty();
    }

    private static TestScheduler CreateTestScheduler()
    {
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks);

        return scheduler;
    }

    private static Func<Item, TimeSpan?> CreateTimeSelector(IScheduler scheduler)
        => item => item.Expiration - scheduler.Now;

    private static DateTimeOffset PerformStressEdits(
        ISourceList<Item> source,
        IScheduler scheduler,
        int stressCount,
        TimeSpan minItemLifetime,
        TimeSpan maxItemLifetime,
        int maxChangeCount,
        int maxRangeSize)
    {
        var nextItemId = 1;
        var randomizer = new Randomizer(1234567);
        var maxExpiration = DateTimeOffset.MinValue;

        for (var i = 0; i < stressCount; ++i)
            source.Edit(mutator =>
            {
                var changeCount = randomizer.Int(1, maxChangeCount);

                for (var i = 0; i < changeCount; ++i)
                {
                    var changeReason = mutator.Count switch
                    {
                        0 => randomizer.Enum(exclude: new[]
                        {
                            ListChangeReason.Replace,
                            ListChangeReason.Remove,
                            ListChangeReason.RemoveRange,
                            ListChangeReason.Refresh,
                            ListChangeReason.Moved,
                            ListChangeReason.Clear
                        }),
                        1 => randomizer.Enum(exclude: new[]
                        {
                            ListChangeReason.Refresh,
                            ListChangeReason.Moved
                        }),
                        _ => randomizer.Enum(exclude: ListChangeReason.Refresh)
                    };

                    switch (changeReason)
                    {
                        case ListChangeReason.Add:
                            mutator.Add(new Item()
                            {
                                Id = nextItemId++,
                                Expiration = GenerateExpiration()
                            });
                            break;

                        case ListChangeReason.AddRange:
                            mutator.AddRange(Enumerable
                                .Range(0, randomizer.Int(1, maxRangeSize))
                                .Select(_ => new Item()
                                {
                                    Id = nextItemId++,
                                    Expiration = GenerateExpiration()
                                })
                                .ToArray());
                            break;

                        case ListChangeReason.Replace:
                            mutator.Replace(
                                original: randomizer.ListItem(mutator),
                                replaceWith: new Item()
                                {
                                    Id = nextItemId++,
                                    Expiration = GenerateExpiration()
                                });
                            break;

                        case ListChangeReason.Remove:
                            mutator.RemoveAt(randomizer.Int(0, mutator.Count - 1));
                            break;

                        case ListChangeReason.RemoveRange:
                            var removeCount = randomizer.Int(1, Math.Min(maxRangeSize, mutator.Count));
                            mutator.RemoveRange(
                                index: randomizer.Int(0, mutator.Count - removeCount),
                                count: removeCount);
                            break;

                        case ListChangeReason.Moved:
                            int originalIndex;
                            int destinationIndex;

                            do
                            {
                                originalIndex = randomizer.Int(0, mutator.Count - 1);
                                destinationIndex = randomizer.Int(0, mutator.Count - 1);
                            } while (originalIndex != destinationIndex);

                            mutator.Move(originalIndex, destinationIndex);
                            break;

                        case ListChangeReason.Clear:
                            mutator.Clear();
                            break;
                    }
                }
            });

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

    private class Item
    {
        public required int Id { get; init; }

        public DateTimeOffset? Expiration { get; set; }
    }
}
