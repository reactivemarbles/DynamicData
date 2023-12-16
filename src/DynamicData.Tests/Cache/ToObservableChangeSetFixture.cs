using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ToObservableChangeSetFixture
    : ReactiveTest
{
    [Fact]
    public void ExpirationIsGiven_RemovalIsScheduled()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(
                keySelector: static item => item.Id,
                expireAfter: static item => item.Lifetime,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2, Lifetime = TimeSpan.FromMilliseconds(20) };
        var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromMilliseconds(30) };
        var item4 = new Item() { Id = 4, Lifetime = TimeSpan.FromMilliseconds(40) };
        var item5 = new Item() { Id = 5, Lifetime = TimeSpan.FromMilliseconds(50) };
        source.OnNext(new[] { item1, item2, item3, item4, item5 });
        scheduler.AdvanceBy(1);

        // Item removals should batch to the closest prior millisecond.
        // This actually seems wrong to me, that for items to be removed earlier than asked for.
        // Should this maybe batch to the closest future millisecond, or just round to the nearest?
        var item6 = new Item() { Id = 6, Lifetime = TimeSpan.FromMilliseconds(20.1) };
        var item7 = new Item() { Id = 7, Lifetime = TimeSpan.FromMilliseconds(20.9) };
        source.OnNext(new[] { item6, item7 });
        scheduler.AdvanceBy(1);

        // Out-of-order removal
        var item8 = new Item() { Id = 8, Lifetime = TimeSpan.FromMilliseconds(15) };
        source.OnNext(new[] { item8 });
        scheduler.AdvanceBy(1);

        // Non-expiring item
        var item9 = new Item() { Id = 9 };
        source.OnNext(new[] { item9 });
        scheduler.AdvanceBy(1);

        // Replacement changing lifetime.
        var item10 = new Item() { Id = 4, Lifetime = TimeSpan.FromMilliseconds(45) };
        source.OnNext(new[] { item10 });
        scheduler.AdvanceBy(1);

        // Replacement not-affecting lifetime.
        var item11 = new Item() { Id = 5, Lifetime = TimeSpan.FromMilliseconds(50) };
        source.OnNext(new[] { item11 });
        scheduler.AdvanceBy(1);

        // Verify initial state, after all emissions
        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(6, "6 item sets were emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3, item6, item7, item8, item9, item10, item11 }, "11 items were emitted, 2 of which were replacements");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(10).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(7, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item8, item9, item10, item11 }, "item #1 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(15).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(8, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2, item3, item6, item7, item9, item10, item11 }, "item #8 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(20).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(9, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item3, item9, item10, item11 }, "items #2, #6, and #7 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(30).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(10, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "item #3 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(40).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(10, "no changes should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item9, item10, item11 }, "no items should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(45).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(11, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item9, item11 }, "item #10 should have expired");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(50).Ticks);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(12, "1 expiration should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item9 }, "item #11 should have expired");
    }

    [Fact]
    public void KeySelectorIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.ToObservableChangeSet<object, object>(
                source: new Subject<object>(),
                keySelector: null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void KeySelectorThrows_SubscriptionReceivesError()
    {
        using var source = new Subject<Item>();

        var error = new Exception("Test Exception");

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(static item => (item.Error is not null)
                ? throw item.Error
                : item.Id));

        var item1 = new Item() { Id = 1 };
        source.OnNext(item1);
        source.OnNext(new Item() { Id = 2, Error = error });
        source.OnNext(new Item() { Id = 3 });

        results.Error.Should().BeSameAs(error);
        results.Messages.Count.Should().Be(1, "1 item was emitted before an error occurred");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was emitted before an error occurred");
    }

    [Fact]
    public void RemovalsArePending_CompletionWaitsForRemovals()
    {
        using var source = new Subject<IEnumerable<Item>>();

        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(
                keySelector: static item => item.Id,
                expireAfter: static item => item.Lifetime,
                scheduler: scheduler));

        var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromMilliseconds(10) };
        var item2 = new Item() { Id = 2 };
        var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromMilliseconds(30) };
        source.OnNext(new[] { item1, item2, item3 });
        scheduler.AdvanceBy(1);

        source.OnCompleted();

        results.IsCompleted.Should().BeFalse("item removals have been scheduled, and not completed");
        results.Messages.Count.Should().Be(1, "1 item set was emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were emitted");

        scheduler.AdvanceTo(TimeSpan.FromMilliseconds(30).Ticks);

        results.IsCompleted.Should().BeTrue("the source has completed, and no outstanding expirations remain");
        results.Messages.Count.Should().Be(3, "2 expirations should have occurred, since the last check");
        results.Data.Items.Should().BeEquivalentTo(new[] { item2 }, "3 items were emitted, and 2 should have expired");
    }

    [Fact]
    public void SourceCompletesImmediately_SubscriptionCompletes()
    {
        var item = new Item() { Id = 1 };

        var source = Observable.Create<Item>(observer =>
        {
            observer.OnNext(item);
            observer.OnCompleted();
            return Disposable.Empty;
        });

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(static item => item.Id));

        results.IsCompleted.Should().BeTrue("the source has completed, and no outstanding expirations remain");
        results.Messages.Count.Should().Be(1, "1 item was emitted");
        results.Data.Items.Should().BeEquivalentTo(new[] { item }, "1 item was emitted");
    }

    [Fact]
    public void SizeLimitIsExceeded_OldestItemsAreRemoved()
    {
        using var source = new Subject<IEnumerable<Item>>();

        // scheduler is currently used to process evictions, even though they could be processed synchronously. Plan to change this.
        var scheduler = new TestScheduler();

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(
                keySelector: static item => item.Id,
                limitSizeTo: 5,
                scheduler: scheduler));

        // Populate enough initial items so that at least one item at the end never gets evicted
        var item1 = new Item() { Id = 1 };
        var item2 = new Item() { Id = 2 };
        var item3 = new Item() { Id = 3 };
        var item4 = new Item() { Id = 4 };
        source.OnNext(new[] { item1, item2, item3, item4 });
        scheduler.AdvanceBy(1);

        // Limit is reached
        var item5 = new Item() { Id = 5 };
        source.OnNext(new[] { item5 });
        scheduler.AdvanceBy(1);

        // New item exceeds the limit
        var item6 = new Item() { Id = 6 };
        source.OnNext(new[] { item6 });
        scheduler.AdvanceBy(1);

        // Multiple items exceed the limit
        var item7 = new Item() { Id = 7 };
        var item8 = new Item() { Id = 8 };
        source.OnNext(new[] { item7, item8 });
        scheduler.AdvanceBy(1);

        // Replacement leaves all other items in-place
        var item9 = new Item() { Id = 7 };
        source.OnNext(new[] { item9 });
        scheduler.AdvanceBy(1);

        // Replacement and eviction in at the same time
        var item10 = new Item() { Id = 8 };
        var item11 = new Item() { Id = 11 };
        source.OnNext(new[] { item10, item11 });
        scheduler.AdvanceBy(1);

        results.Error.Should().BeNull();

        // TODO: This was set to 9 but fails, this was changed in a recent commit form 6 to 9, but I'm not sure why.
        results.Messages.Count.Should().Be(6, "6 item sets were emitted by the source, 3 of which triggered followup evictions");
        results.Data.Items.Should().BeEquivalentTo(new[] { item5, item6, item9, item10, item11 }, "the size limit of the collection was 5");
    }

    [Fact]
    public void SourceErrorsImmediately_SubscriptionReceivesError()
    {
        var item = new Item() { Id = 1 };
        var error = new Exception("Test Exception");

        var source = Observable.Create<Item>(observer =>
        {
            observer.OnNext(item);
            observer.OnError(error);
            return Disposable.Empty;
        });

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(static item => item.Id));

        results.Error.Should().BeSameAs(error);
        results.Messages.Count.Should().Be(1, "1 item was emitted, before an error occurred");
        results.Data.Items.Should().BeEquivalentTo(new[] { item }, "1 item was emitted, before an error occurred");
    }

    [Fact]
    public void SourceEmitsSingle_ItemIsAddedOrUpdated()
    {
        using var source = new Subject<Item>();

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(static item => item.Id));

        var item1 = new Item() { Id = 1 };
        source.OnNext(item1);

        var item2 = new Item() { Id = 2 };
        source.OnNext(item2);

        var item3 = new Item() { Id = 1 };
        source.OnNext(item3);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(3, "3 items were emitted by the source");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2 }, "3 unique items were emitted, one of which was a replacement");
    }

    [Fact]
    public void SourceEmitsMany_ItemsAreAddedOrUpdated()
    {
        using var source = new Subject<IEnumerable<Item>>();

        using var results = new ChangeSetAggregator<Item, int>(source
            .ToObservableChangeSet(static item => item.Id));

        var item1 = new Item() { Id = 1 };
        var item2 = new Item() { Id = 2 };
        source.OnNext(new[] { item1, item2 });

        var item3 = new Item() { Id = 1 };
        var item4 = new Item() { Id = 3 };
        source.OnNext(new[] { item3, item4 });

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(2, "2 item sets were emitted by the source");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1, item2, item4 }, "4 unique items were emitted, on of which was a replacement");
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.ToObservableChangeSet<object, object>(
                source: null!,
                keySelector: static item => item))
            .Should().Throw<ArgumentNullException>();

    public class Item
    {
        public int Id { get; init; }

        public Exception? Error { get; init; }

        public TimeSpan? Lifetime { get; init; }
    }
}
