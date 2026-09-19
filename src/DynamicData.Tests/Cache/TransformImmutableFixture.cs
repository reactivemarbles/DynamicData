namespace DynamicData.Tests.Cache;

public sealed class TransformImmutableFixture
{
    [Test]
    public async Task ItemsAreManipulated_ItemsAreTransformed()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

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

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Messages.ElementAt(0).Select(change => change.CurrentIndex)).IsEquivalentTo(operation1.Select(change => change.CurrentIndex)).Because("indexes should be preserved");
        await Assert.That(results.Messages.ElementAt(0).Select(change => change.PreviousIndex)).IsEquivalentTo(operation1.Select(change => change.PreviousIndex)).Because("indexes should be preserved");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1.Name, item2.Name }).Because("2 items were added");

        // Replace items, changing inclusion
        var item3 = new Item() { Id = item1.Id, Name = "Item #3" };
        var item4 = new Item() { Id = item2.Id, Name = "Item #4" };
        var operation2 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Update, key: item3.Id, current: item3, previous: item1, currentIndex: 0, previousIndex: 0),
            new(reason: ChangeReason.Update, key: item4.Id, current: item4, previous: item2, currentIndex: 1, previousIndex: 1)
        };
        source.OnNext(operation2);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Messages.ElementAt(1).Select(change => change.CurrentIndex)).IsEquivalentTo(operation2.Select(change => change.CurrentIndex)).Because("indexes should be preserved");
        await Assert.That(results.Messages.ElementAt(1).Select(change => change.PreviousIndex)).IsEquivalentTo(operation2.Select(change => change.PreviousIndex)).Because("indexes should be preserved");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item3.Name, item4.Name }).Because("2 items were replaced");

        // Refresh items
        var operation3 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Refresh, key: item3.Id, current: item3, index: 0),
            new(reason: ChangeReason.Refresh, key: item4.Id, current: item4, index: 1)
        };
        source.OnNext(operation3);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Messages.ElementAt(2).Select(change => change.CurrentIndex)).IsEquivalentTo(operation3.Select(change => change.CurrentIndex)).Because("indexes should be preserved");
        await Assert.That(results.Messages.ElementAt(2).Select(change => change.PreviousIndex)).IsEquivalentTo(operation3.Select(change => change.PreviousIndex)).Because("indexes should be preserved");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item3.Name, item4.Name }).Because("2 items were refreshed");

        // Move items
        var operation4 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Moved, key: item3.Id, current: item3, previous: default, currentIndex: 1, previousIndex: 0),
            new(reason: ChangeReason.Moved, key: item4.Id, current: item4, previous: default, currentIndex: 1, previousIndex: 0)
        };
        source.OnNext(operation4);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Skip(3).Count()).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Messages.ElementAt(3).Select(change => change.CurrentIndex)).IsEquivalentTo(operation4.Select(change => change.CurrentIndex)).Because("indexes should be preserved");
        await Assert.That(results.Messages.ElementAt(3).Select(change => change.PreviousIndex)).IsEquivalentTo(operation4.Select(change => change.PreviousIndex)).Because("indexes should be preserved");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item4.Name, item3.Name }).Because("2 items were moved");

        // Remove items
        var operation5 = new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Remove, key: item3.Id, current: item3, index: 0),
            new(reason: ChangeReason.Remove, key: item4.Id, current: item4, index: 1)
        };
        source.OnNext(operation5);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Skip(4).Count()).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Messages.ElementAt(4).Select(change => change.CurrentIndex)).IsEquivalentTo(operation5.Select(change => change.CurrentIndex)).Because("indexes should be preserved");
        await Assert.That(results.Messages.ElementAt(4).Select(change => change.PreviousIndex)).IsEquivalentTo(operation5.Select(change => change.PreviousIndex)).Because("indexes should be preserved");
        await Assert.That(results.Data.Items).IsEmpty().Because("2 items were removed");

        await Assert.That(results.IsCompleted).IsFalse();
    }

    [Test]
    public async Task SourceCompletes_CompletionIsPropagated()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

        using var results = source
            .TransformImmutable(transformFactory: Item.NameSelector)
            .AsAggregator();

        var item1 = new Item() { Id = 1, Name = "Item #1" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });
        source.OnCompleted();

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.IsCompleted).IsTrue();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1.Name }).Because("1 item was added");

        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, Name = "Item #2" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        await Assert.That(results.Messages.Skip(1)).IsEmpty().Because("no source operations should have been processed");
    }

    [Test]
    public async Task SourceCompletesImmediately_CompletionIsPropagated()
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

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.IsCompleted).IsTrue();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1.Name }).Because("1 item was added");
    }

    [Test]
    public async Task SourceErrors_ErrorIsPropagated()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

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

        await Assert.That(results.Error).IsEqualTo(error);
        await Assert.That(results.IsCompleted).IsFalse();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1.Name }).Because("1 item was added");

        // Make sure no extraneous notifications are published.
        var item2 = new Item() { Id = 2, Name = "Item #2" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item2.Id, current: item2)
        });

        await Assert.That(results.Messages.Skip(1)).IsEmpty().Because("no source operations should have been processed");
    }

    [Test]
    public async Task SourceErrorsImmediately_ErrorIsPropagated()
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

        await Assert.That(results.Error).IsEqualTo(error);
        await Assert.That(results.IsCompleted).IsFalse();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1.Name }).Because("1 item was added");
    }

    [Test]
    public async Task SourceIsNull_ThrowsException()
        => await Assert.That(() => ObservableCacheEx.TransformImmutable(
            source: (null as IObservable<IChangeSet<Item, int>>)!,
            transformFactory: Item.NameSelector)).Throws<ArgumentNullException>();

    [Test]
    public async Task TransformFactoryIsNull_ThrowsException()
        => await Assert.That(() => Observable
                .Never<IChangeSet<Item, int>>()
                .TransformImmutable<string, Item, int>(transformFactory: null!)).Throws<ArgumentNullException>();

    [Test]
    public async Task TransformFactoryThrows_ExceptionIsCaptured()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

        var error = new Exception();

        using var results = source
            .TransformImmutable<string, Item, int>(transformFactory: _ => throw error)
            .AsAggregator();

        var item1 = new Item() { Id = 1, Name = "Item #1" };
        source.OnNext(new ChangeSet<Item, int>()
        {
            new(reason: ChangeReason.Add, key: item1.Id, current: item1)
        });

        await Assert.That(results.Error).IsEqualTo(error);
        await Assert.That(results.Messages).IsEmpty().Because("no source operations should have been processed");
        await Assert.That(results.IsCompleted).IsFalse();
    }

    // https://github.com/reactivemarbles/DynamicData/issues/925
    [Test]
    public async Task TDestinationIsValueType_DoesNotThrowException()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<string, string>>();

        using var results = source
            .TransformImmutable(transformFactory: static value => value.Length)
            .AsAggregator();

        source.OnNext(new ChangeSet<string, string>()
        {
            new(reason: ChangeReason.Add, key: "Item #1", current: "Item #1", index: 0)
        });

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("1 source operation was performed");
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
