using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.List;

public class ToObservableChangeSetFixture
    : ReactiveTest
{
    [Fact]
    public void ExpirationIsGiven_RemovalIsScheduled()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                expireAfter: static item => item.Lifetime,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2, Lifetime = TimeSpan.FromMilliseconds(20) };
        var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromMilliseconds(30) };
        source.OnNext(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        // Item removals should batch to the closest prior millisecond.
        // This actually seems wrong to me, that for items to be removed earlier than asked for.
        // Should this maybe batch to the closest future millisecond, or just round to the nearest?
        var item4 = new Item() { Id = 4, Lifetime = TimeSpan.FromMilliseconds(20.1) };
        var item5 = new Item() { Id = 5, Lifetime = TimeSpan.FromMilliseconds(20.9) };
        source.OnNext(new[] { item4, item5 });
        scheduler.AdvanceBy(1);

        // Out-of-order removal
        var item6 = new Item() { Id = 6, Lifetime = TimeSpan.FromMilliseconds(15) };
        source.OnNext(new[] { item6 });
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item7 = new Item() { Id = 7 };
        source.OnNext(new[] { item7 });
        scheduler.AdvanceBy(1);

        // Verify initial state, after all emissions
        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(4, "4 item sets were emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item4, item5, item6, item7 }, "7 items were emitted");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(10).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(5, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item4, item5, item6, item7 }, "item #1 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(15).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(6, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item4, item5, item7 }, "item #6 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(20).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(7, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item3, item7 }, "items #2, #4, and #5 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(30).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(8, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item7 }, "item #3 should have expired");
    }

    [Fact]
    public void ItemIsEvictedBeforeExpiration_ExpirationIsCancelled()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                expireAfter: static item => item.Lifetime,
                limitSizeTo: 3,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromMilliseconds(10) };
        source.OnNext(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        var item4 = new Item() {Id = 4 };
        source.OnNext(new[] { item4 });
        scheduler.AdvanceBy(1);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(2, "2 item sets were emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item4 }, "the size limit of the collection was 3");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(10).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(3, "2 items should have expired, at the same time, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item4 }, "2 items should have expired, since the last check");
    }

    [Fact]
    public void ItemExpiresBeforeEviction_EvictionIsSkipped()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                expireAfter: static item => item.Lifetime,
                limitSizeTo: 3,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2 };
        var item3 = new Item() { Id = 3 };
        source.OnNext(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(1, "1 item set was emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "the size limit of the collection was 3");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(10).Ticks);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(2, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3 }, "item #1 should have expired");

        var item4 = new Item() { Id = 4 };
        source.OnNext(new[] { item4 });
        scheduler.AdvanceBy(1);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(3, "1 item set was emitted, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item4 }, "no eviction should have occurred");
    }

    [Fact]
    public void LimitToSizeIs0_ChangeSetsAreEmpty()
    {
        using var source = new Subject<Item>();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                limitSizeTo: 0));

        var item1 = new Item() { Id = 1 };
        source.OnNext(item1);

        var item2 = new Item() { Id = 2 };
        source.OnNext(item2);

        var item3 = new Item() { Id = 3 };
        source.OnNext(item3);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(3, "3 items were emitted");
        results.Data.Items.Should().BeEmpty("the size limit of the collection was 0");
    }

    [Fact]
    public void RemovalsArePending_CompletionWaitsForRemovals()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                expireAfter: static item => item.Lifetime,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2 };
        var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromMilliseconds(30) };
        source.OnNext(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        source.OnCompleted();

        results.Exception.Should().BeNull();
        results.IsCompleted.Should().BeFalse("item removals have been scheduled, and not completed");
        results.Messages.Count.Should().Be(1, "1 item set was emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were emitted");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(30).Ticks);

        results.Exception.Should().BeNull();
        results.IsCompleted.Should().BeTrue("the source has completed, and no outstanding expirations remain");
        results.Messages.Count.Should().Be(3, "2 expirations should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2 }, "3 items were emitted, and 2 should have expired");
    }

    [Fact]
    public void SourceCompletesImmediately_SubscriptionCompletes()
    {
        var item = new Item();

        var source = Observable.Create<Item>(observer =>
        {
            observer.OnNext(item);
            observer.OnCompleted();
            return Disposable.Empty;
        });

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet());

        results.Exception.Should().BeNull();
        results.IsCompleted.Should().BeTrue("the source has completed, and no outstanding expirations remain");
        results.Messages.Count.Should().Be(1, "1 item was emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item }, "1 item was emitted");
    }

    [Fact]
    public void SizeLimitIsExceeded_OldestItemsAreRemoved()
    {
        using var source = new Subject<IEnumerable<Item>>();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(limitSizeTo: 4));

        // Populate enough initial items so that at least one item at the end never gets evicted
        var item1 = new Item() { Id = 1 };
        var item2 = new Item() { Id = 2 };
        var item3 = new Item() { Id = 3 };
        source.OnNext(new[] { item1, item2, item3 });

        // Limit is reached
        var item4 = new Item() { Id = 4 };
        source.OnNext(new[] { item4 });

        // New item exceeds the limit
        var item5 = new Item() { Id = 5 };
        source.OnNext(new[] { item5 });

        // Multiple items exceed the limit
        var item6 = new Item() { Id = 6 };
        var item7 = new Item() { Id = 7 };
        source.OnNext(new[] { item6, item7 });

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(4, "4 item sets were emitted by the source");
        results.Data.Items.Should().BeEquivalentTo(new[] { item4, item5, item6, item7 }, "the size limit of the collection was 4");
    }

    [Fact]
    public void SourceErrorsImmediately_SubscriptionReceivesError()
    {
        var item = new Item();
        var error = new Exception("Test Exception");

        var source = Observable.Create<Item>(observer =>
        {
            observer.OnNext(item);
            observer.OnError(error);
            return Disposable.Empty;
        });

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet());

        results.Exception.Should().BeSameAs(error);
        results.Messages.Count.Should().Be(1, "1 item was emitted, before an error occurred");
        results.Data.Items.Should().BeEquivalentTo(new[] { item }, "1 item was emitted, before an error occurred");
    }

    [Fact]
    public void SourceEmitsSingle_ItemIsAdded()
    {
        using var source = new Subject<Item>();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet());

        var item1 = new Item() { Id = 1 };
        source.OnNext(item1);

        var item2 = new Item() { Id = 2 };
        source.OnNext(item2);

        var item3 = new Item() { Id = 3 };
        source.OnNext(item3);

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(3, "3 items were emitted by the source");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 },
            config: options => options.WithStrictOrdering(),
            because: "3 items were emitted");
    }

    [Fact]
    public void SourceEmitsMany_ItemsAreAddedOrUpdated()
    {
        using var source = new Subject<IEnumerable<Item>>();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet());

        var item1 = new Item() { Id = 1 };
        var item2 = new Item() { Id = 2 };
        source.OnNext(new[] { item1, item2 });

        var item3 = new Item() { Id = 3 };
        var item4 = new Item() { Id = 4 };
        source.OnNext(new[] { item3, item4 });

        results.Exception.Should().BeNull();
        results.Messages.Count.Should().Be(2, "2 item sets were emitted by the source");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item4 }, "4 items were emitted");
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableListEx.ToObservableChangeSet<object>(source: null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void ThreadPoolSchedulerIsUsed_ExpirationIsThreadSafe()
    {
        var testDuration = TimeSpan.FromSeconds(1);
        var maxItemLifetime = TimeSpan.FromMilliseconds(500);

        using var source = new Subject<Item>();

        using var results = new ChangeSetAggregator<Item>(source
            .ToObservableChangeSet(
                expireAfter: static item => item.Lifetime,
                limitSizeTo: 1000,
                scheduler: ThreadPoolScheduler.Instance));

        var nextItemId = 1;
        var rng = new Random(Seed: 1234567);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        while (stopwatch.Elapsed < testDuration)
        {
            source.OnNext(new()
            {
                Id = nextItemId++,
                Lifetime = TimeSpan.FromMilliseconds(rng.Next(maxItemLifetime.Milliseconds + 1))
            });
        }

        results.Exception.Should().BeNull();
    }

    public class Item
    {
        public int Id { get; set; }

        public TimeSpan? Lifetime { get; set; }
    }
}
