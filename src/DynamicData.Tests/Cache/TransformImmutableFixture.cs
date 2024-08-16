using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class TransformImmutableFixture
{
    [Fact]
    public void ItemsAreManipulated_ItemsAreTransformed()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();


        // Additions
        var item1 = new Item() { Id = 1, Name = "Item #1" };
        var item2 = new Item() { Id = 2, Name = "Item #2" };
        var operation1 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1, index: 0),
            new(reason: ChangeReason.Add, key: item2.Id, current: item2, index: 1)
        };
        source.OnNext(operation1);

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Messages.ElementAt(0).Select(change => change.CurrentIndex).Should().BeEquivalentTo(operation1.Select(change => change.CurrentIndex), "indexes should be preserved");
        results.Messages.ElementAt(0).Select(change => change.PreviousIndex).Should().BeEquivalentTo(operation1.Select(change => change.PreviousIndex), "indexes should be preserved");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1.Name, item2.Name }, "2 items were added");


        // Replace items, changing inclusion
        var item3 = new Item() { Id = item1.Id, Name = "Item #3" };
        var item4 = new Item() { Id = item2.Id, Name = "Item #4" };
        var operation2 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Update, key: item3.Id, current: item3, previous: item1, currentIndex: 0, previousIndex: 0),
            new(reason: ChangeReason.Update, key: item4.Id, current: item4, previous: item2, currentIndex: 1, previousIndex: 1)
        };
        source.OnNext(operation2);

        results.Error.Should().BeNull();
        results.Messages.Skip(1).Count().Should().Be(1, "1 source operation was performed");
        results.Messages.ElementAt(1).Select(change => change.CurrentIndex).Should().BeEquivalentTo(operation2.Select(change => change.CurrentIndex), "indexes should be preserved");
        results.Messages.ElementAt(1).Select(change => change.PreviousIndex).Should().BeEquivalentTo(operation2.Select(change => change.PreviousIndex), "indexes should be preserved");
        results.Data.Items.Should().BeEquivalentTo(new[] { item3.Name, item4.Name }, "2 items were replaced");


        // Refresh items
        var operation3 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Refresh, key: item3.Id, current: item3, index: 0),
            new(reason: ChangeReason.Refresh, key: item4.Id, current: item4, index: 1)
        };
        source.OnNext(operation3);

        results.Error.Should().BeNull();
        results.Messages.Skip(2).Count().Should().Be(1, "1 source operation was performed");
        results.Messages.ElementAt(2).Select(change => change.CurrentIndex).Should().BeEquivalentTo(operation3.Select(change => change.CurrentIndex), "indexes should be preserved");
        results.Messages.ElementAt(2).Select(change => change.PreviousIndex).Should().BeEquivalentTo(operation3.Select(change => change.PreviousIndex), "indexes should be preserved");
        results.Data.Items.Should().BeEquivalentTo(new[] { item3.Name, item4.Name }, "2 items were refreshed");


        // Move items
        var operation4 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Moved, key: item3.Id, current: item3, previous: default, currentIndex: 1, previousIndex: 0),
            new(reason: ChangeReason.Moved, key: item4.Id, current: item4, previous: default, currentIndex: 1, previousIndex: 0)
        };
        source.OnNext(operation4);

        results.Error.Should().BeNull();
        results.Messages.Skip(3).Count().Should().Be(1, "1 source operation was performed");
        results.Messages.ElementAt(3).Select(change => change.CurrentIndex).Should().BeEquivalentTo(operation4.Select(change => change.CurrentIndex), "indexes should be preserved");
        results.Messages.ElementAt(3).Select(change => change.PreviousIndex).Should().BeEquivalentTo(operation4.Select(change => change.PreviousIndex), "indexes should be preserved");
        results.Data.Items.Should().BeEquivalentTo(new[] { item4.Name, item3.Name }, "2 items were moved");


        // Remove items
        var operation5 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Remove, key: item3.Id, current: item3, index: 0),
            new(reason: ChangeReason.Remove, key: item4.Id, current: item4, index: 1)
        };
        source.OnNext(operation5);

        results.Error.Should().BeNull();
        results.Messages.Skip(4).Count().Should().Be(1, "1 source operation was performed");
        results.Messages.ElementAt(4).Select(change => change.CurrentIndex).Should().BeEquivalentTo(operation5.Select(change => change.CurrentIndex), "indexes should be preserved");
        results.Messages.ElementAt(4).Select(change => change.PreviousIndex).Should().BeEquivalentTo(operation5.Select(change => change.PreviousIndex), "indexes should be preserved");
        results.Data.Items.Should().BeEmpty("2 items were removed");


        results.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void SourceCompletes_CompletionIsPropagated()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var results = source
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();


        var item1 = new Item() { Id = 1, Name = "Item #1" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });
        source.OnCompleted();

        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeTrue();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1.Name }, "1 item was added");

        
        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, Name = "Item #2" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        results.Messages.Skip(1).Should().BeEmpty("no source operations should have been processed");
    }

    [Fact]
    public void SourceCompletesImmediately_CompletionIsPropagated()
    {
        var item1 = new Item() { Id = 1, Name = "Item #1" };

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
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();


        results.Error.Should().BeNull();
        results.IsCompleted.Should().BeTrue();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1.Name }, "1 item was added");
    }

    [Fact]
    public void SourceErrors_ErrorIsPropagated()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        var error = new Exception();

        using var results = source
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();


        var item1 = new Item() { Id = 1, Name = "Item #1" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });
        source.OnError(error);

        results.Error.Should().Be(error);
        results.IsCompleted.Should().BeFalse();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1.Name }, "1 item was added");

        
        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, Name = "Item #2" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        results.Messages.Skip(1).Should().BeEmpty("no source operations should have been processed");
    }

    [Fact]
    public void SourceErrorsImmediately_ErrorIsPropagated()
    {
        var item1 = new Item() { Id = 1, Name = "Item #1" };
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
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();


        results.Error.Should().Be(error);
        results.IsCompleted.Should().BeFalse();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
        results.Data.Items.Should().BeEquivalentTo(new[] { item1.Name }, "1 item was added");
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.TransformImmutable(
            source: (null as IObservable<IChangeSet<Item, int>>)!,
            transformFactory: Item.NameSelector))
        .Should().Throw<ArgumentNullException>();

    [Fact]
    public void TransformFactoryIsNull_ThrowsException()
        => FluentActions.Invoking(() => Observable
                .Never<IChangeSet<Item, int>>()
                .TransformImmutable<string, Item, int>(transformFactory: null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void TransformFactoryThrows_ExceptionIsCaptured()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        var error = new Exception();

        using var results = source
            .TransformImmutable<string, Item, int>(transformFactory: _ => throw error)
            .AsAggregator();


        var item1 = new Item() { Id = 1, Name = "Item #1" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });

        results.Error.Should().Be(error);
        results.Messages.Should().BeEmpty("no source operations should have been processed");
        results.IsCompleted.Should().BeFalse();
    }

    // https://github.com/reactivemarbles/DynamicData/issues/925
    [Fact]
    public void TDestinationIsValueType_DoesNotThrowException()
    {
        using var source = new Subject<IChangeSet<string, string>>();

        using var results = source
            .TransformImmutable(transformFactory: static value => value.Length)
            .AsAggregator();


        source.OnNext(new ChangeSet<string, string>()
        {
            new(reason: ChangeReason.Add, key: "Item #1", current: "Item #1", index: 0)
        });

        results.Error.Should().BeNull();
        results.Messages.Count.Should().Be(1, "1 source operation was performed");
    }

    private class Item
    {
        public static readonly Func<Item, int> IdSelector
            = item => item.Id;

        public static readonly Func<Item, string> NameSelector
            = item => item.Name;

        public required int Id { get; init; }

        public required string Name { get; init; }
    }
}
