using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class FilterImmutableFixture
{
    [Fact]
    public void ItemsAreManipulated_UnmatchedItemsAreExcludedAndIndexesAreDiscarded()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        // Add items
        var item1 = new Item() { Id = 1, IsIncluded = true };
        var item2 = new Item() { Id = 2, IsIncluded = false };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
            new(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "2 items were added, with 1 excluded");


        // Replace items, changing inclusion
        var item3 = new Item() { Id = item1.Id, IsIncluded = false };
        var item4 = new Item() { Id = item2.Id, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Update, key: item3.Id, current: item3, previous: item1, currentIndex: 0, previousIndex: 0),
            new(reason: ChangeReason.Update, key: item4.Id, current: item4, previous: item2, currentIndex: 1, previousIndex: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Skip(1).Count().Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item4 }, "2 items were replaced, with 1 excluded");


        // Replace items, not changing inclusion
        var item5 = new Item() { Id = item3.Id, IsIncluded = false };
        var item6 = new Item() { Id = item4.Id, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Update, key: item5.Id, current: item5, previous: item3, currentIndex: 0, previousIndex: 0),
            new(reason: ChangeReason.Update, key: item6.Id, current: item6, previous: item4, currentIndex: 1, previousIndex: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Skip(2).Count().Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item6 }, "2 items were replaced, with 1 excluded");


        // Refresh items
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Refresh, key: item5.Id, current: item5, index: 0),
            new(reason: ChangeReason.Refresh, key: item6.Id, current: item6, index: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Skip(3).Count().Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item6 }, "2 items were refreshed, with 1 excluded");


        // Remove items
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Remove, key: item5.Id, current: item5, index: 0),
            new(reason: ChangeReason.Remove, key: item6.Id, current: item6, index: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Skip(4).Count().Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEmpty("2 items were removed, with one excluded");


        results.Messages.SelectMany(static changes => changes).Should().AllSatisfy(
            change =>
            {
                change.CurrentIndex.Should().Be(-1);
                change.PreviousIndex.Should().Be(-1);
            },
            because: "indexes should not be preserved");
        results.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void ItemsAreMoved_ChangesAreNotPropagated()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();

        // Initial setup
        var item1 = new Item() { Id = 1, IsIncluded = true };
        var item2 = new Item() { Id = 2, IsIncluded = true };
        var item3 = new Item() { Id = 3, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
            new(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1),
            new(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 2)
        });
        results.Messages.Clear();


        // Move items
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Moved, key: item1.Id, current: item1, previous: default, currentIndex: 2, previousIndex: 0),
            new(reason: ChangeReason.Moved, key: item2.Id, current: item1, previous: default, currentIndex: 0, previousIndex: 1)
        });

        results.Error.Should().BeNull();
        results.Messages.Should().BeEmpty("move operations should not be propagated");
    }

    [Fact]
    public void PredicateIsNull_ThrowsException()
        => FluentActions.Invoking(() => Observable
                .Never<IChangeSet<Item, int>>()
                .FilterImmutable(predicate: null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void PredicateThrows_ExceptionIsCaptured()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        var error = new Exception();

        using var results = source
            .FilterImmutable(predicate: _ => throw error)
            .AsAggregator();


        var item1 = new Item() { Id = 1, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });

        results.Error.Should().Be(error);
        results.Messages.Should().BeEmpty("no source operations should have been processed");
        results.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void SourceCompletes_CompletionIsPropagated()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        var item1 = new Item() { Id = 1, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });
        source.OnCompleted();

        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeTrue();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

        
        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        results.Messages.Skip(1).Should().BeEmpty("no source operations should have been processed");
    }

    [Fact]
    public void SourceCompletesImmediately_CompletionIsPropagated()
    {
        var item1 = new Item() { Id = 1, IsIncluded = true };

        var source = Observable.Create<IChangeSet<Item, int>>(observer =>
        {
            observer.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });

            observer.OnCompleted();

            return Disposable.Empty;
        });

        var error = new Exception();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeTrue();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");
    }

    [Fact]
    public void SourceErrors_ErrorIsPropagated()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        var error = new Exception();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        var item1 = new Item() { Id = 1, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });
        source.OnError(error);

        results.Error.Should().Be(error);
        results.IsCompleted.Should().BeFalse();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");

        
        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, IsIncluded = true };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        results.Messages.Skip(1).Should().BeEmpty("no source operations should have been processed");
    }

    [Fact]
    public void SourceErrorsImmediately_ErrorIsPropagated()
    {
        var item1 = new Item() { Id = 1, IsIncluded = true };
        var error = new Exception();

        var source = Observable.Create<IChangeSet<Item, int>>(observer =>
        {
            observer.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: item1.Id, current: item1)
            });

            observer.OnError(error);

            return Disposable.Empty;
        });

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        results.Error.Should().Be(error);
        results.IsCompleted.Should().BeFalse();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1 }, "1 item was added");
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.FilterImmutable(
            source: (null as IObservable<IChangeSet<Item, int>>)!,
            predicate: Item.Predicate))
        .Should().Throw<ArgumentNullException>();

    [Fact]
    public void SuppressEmptyChangesetsIsFalse_EmptyChangesetsArePublished()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .FilterImmutable(
                predicate: Item.Predicate,
                suppressEmptyChangeSets: false)
            .AsAggregator();


        ManipulateExcludedItems(source);

        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeFalse();
        results.Messages.Count.Should().Be(5, "5 source operations were performed");
        results.Messages.Should().AllSatisfy(changes => changes.Should().BeEmpty(), "no included items were manipulated");
    }

    [Fact]
    public void SuppressEmptyChangesetsIsTrue_EmptyChangesetsAreNotPublished()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .FilterImmutable(predicate: Item.Predicate)
            .AsAggregator();


        ManipulateExcludedItems(source);

        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeFalse();
        results.Messages.Should().BeEmpty("no source operations should have generated changes");
    }

    private static void ManipulateExcludedItems(ISubject<IChangeSet<Item, int>> source)
    {
        var item1 = new Item() { Id = 1, IsIncluded = false };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0)
        });

        var item2 = new Item() { Id = item1.Id, IsIncluded = false };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Update, key: item2.Id, current: item2, previous: item1, currentIndex: 0, previousIndex: 0)
        });

        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Refresh, key: item2.Id, current: item2, index: 0)
        });

        var item3 = new Item() { Id = 2, IsIncluded = false };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item3.Id, current: item3, index: 1),
            new(reason: ChangeReason.Moved, key: item2.Id, current: item2, previous: default, currentIndex: 1, previousIndex: 0)
        });

        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Remove, key: item2.Id, current: item2, index: 1),
            new(reason: ChangeReason.Remove, key: item3.Id, current: item3, index: 0)
        });
    }

    private class Item
    {
        public static readonly Func<Item, int> KeySelector
            = item => item.Id;

        public static readonly Func<Item, bool> Predicate
            = item => item.IsIncluded;

        public required int Id { get; init; }

        public required bool IsIncluded { get; init; }
    }
}
