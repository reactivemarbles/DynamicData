#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Cache;

public sealed class CacheConvenienceCoverageFixture
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task StartWithEmptyPreservesSpecializedStreamAndCompletion(int kind)
    {
        switch (kind)
        {
            case 0:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source,
                        new ChangeSet<int, int> { new(ChangeReason.Add, 1, 10) }, static changes => changes.Count);
                }

                break;
            case 1:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<ISortedChangeSet<int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, SortedChangeSet<int, int>.Empty, static changes => changes.Count);
                }

                break;
            case 2:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IVirtualChangeSet<int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, VirtualChangeSet<int, int>.Empty, static changes => changes.Count);
                }

                break;
            case 3:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IPagedChangeSet<int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, PagedChangeSet<int, int>.Empty, static changes => changes.Count);
                }

                break;
            case 4:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IGroupChangeSet<int, int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, GroupChangeSet<int, int, int>.Empty, static changes => changes.Count);
                }

                break;
            case 5:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IImmutableGroupChangeSet<int, int, int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, ImmutableGroupChangeSet<int, int, int>.Empty, static changes => changes.Count);
                }

                break;
            case 6:
                using (var source = new ReactiveUI.Primitives.Signals.Signal<IReadOnlyCollection<int>>())
                {
                    await CheckInitialEmpty(source.StartWithEmpty(), source, new[] { 10, 20 }, static items => items.Count);
                }

                break;
        }
    }

    [Test]
    public async Task EqualityAwareAddOrUpdateSuppressesOnlyEqualItems()
    {
        using var cache = new SourceCache<Item, int>(static item => item.Key);
        using var results = cache.Connect().AsAggregator();
        var original = new Item(1, 10);

        cache.AddOrUpdate(original, EqualityComparer<Item>.Default);
        cache.AddOrUpdate(new Item(1, 10), EqualityComparer<Item>.Default);
        cache.AddOrUpdate(new[] { new Item(1, 10), new Item(2, 20) }, EqualityComparer<Item>.Default);
        cache.AddOrUpdate(new[] { new Item(1, 30), new Item(2, 20) }, EqualityComparer<Item>.Default);

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[2].Updates).IsEqualTo(1);
        await Assert.That(results.Messages[2].Single().Previous.Value).IsSameReferenceAs(original);
        await Assert.That(cache.Items.OrderBy(static item => item.Key)).IsEquivalentTo(new[] { new Item(1, 30), new Item(2, 20) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task IntermediateConvenienceMutationPublishesExplicitKeyAndClear()
    {
        using var cache = new IntermediateCache<Item, string>();
        using var results = cache.Connect().AsAggregator();
        var first = new Item(1, 10);
        var second = new Item(2, 20);

        cache.AddOrUpdate(first, "external-key");
        cache.AddOrUpdate(second, "external-key");
        cache.Clear();

        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Single().Key).IsEqualTo("external-key");
        await Assert.That(results.Messages[1].Single().Previous.Value).IsSameReferenceAs(first);
        await Assert.That(results.Messages[2].Single().Current).IsSameReferenceAs(second);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(1);
        await Assert.That(cache.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PopulateFromBatchesAndItemsUnsubscribesWithoutClearingCache()
    {
        using var cache = new SourceCache<Item, int>(static item => item.Key);
        using var batches = new ReactiveUI.Primitives.Signals.Signal<IEnumerable<Item>>();
        using var items = new ReactiveUI.Primitives.Signals.Signal<Item>();
        using var batchSubscription = cache.PopulateFrom(batches);
        using var itemSubscription = cache.PopulateFrom(items);
        using var results = cache.Connect().AsAggregator();

        batches.OnNext(new[] { new Item(1, 10), new Item(2, 20) });
        items.OnNext(new Item(1, 30));
        batchSubscription.Dispose();
        itemSubscription.Dispose();
        batches.OnNext(new[] { new Item(3, 40) });
        items.OnNext(new Item(4, 50));

        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(2);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(1);
        await Assert.That(cache.Items.OrderBy(static item => item.Key)).IsEquivalentTo(new[] { new Item(1, 30), new Item(2, 20) }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(batches.HasObservers).IsFalse();
        await Assert.That(items.HasObservers).IsFalse();
    }

    [Test]
    public async Task PopulateIntoIntermediateAndLockFreeCachesClonesRemovalsAndDisposes()
    {
        using var source = new SourceCache<Item, int>(static item => item.Key);
        using var intermediate = new IntermediateCache<Item, int>();
        using var lockFree = new LockFreeObservableCache<Item, int>();
        using var first = source.Connect().PopulateInto(intermediate);
        using var second = source.Connect().PopulateInto(lockFree);

        source.AddOrUpdate(new[] { new Item(1, 10), new Item(2, 20) });
        source.RemoveKey(1);
        await Assert.That(intermediate.Items).IsEquivalentTo(new[] { new Item(2, 20) });
        await Assert.That(lockFree.Items).IsEquivalentTo(new[] { new Item(2, 20) });

        first.Dispose();
        second.Dispose();
        source.AddOrUpdate(new Item(3, 30));
        await Assert.That(intermediate.Count).IsEqualTo(1);
        await Assert.That(lockFree.Count).IsEqualTo(1);
        lockFree.Clear();
        await Assert.That(lockFree.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ConvertPreservesPreviousValueKeysReasonsAndIndices()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, string>>();
        var messages = new List<IChangeSet<string, string>>();
        using var subscription = source.Convert(static value => $"value:{value}").Subscribe(messages.Add);
        source.OnNext(new ChangeSet<int, string>
        {
            new(ChangeReason.Add, "key", 10),
            new(ChangeReason.Update, "key", 20, ReactiveUI.Primitives.Optional<int>.Create(10), 3, 1),
            new(ChangeReason.Remove, "key", 20)
        });

        await Assert.That(messages.Count).IsEqualTo(1);
        var changes = messages[0].ToArray();
        await Assert.That(changes.Select(static change => change.Reason)).IsEquivalentTo(new[] { ChangeReason.Add, ChangeReason.Update, ChangeReason.Remove }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(changes[1].Key).IsEqualTo("key");
        await Assert.That(changes[1].Current).IsEqualTo("value:20");
        await Assert.That(changes[1].Previous.Value).IsEqualTo("value:10");
        await Assert.That(changes[1].CurrentIndex).IsEqualTo(3);
        await Assert.That(changes[1].PreviousIndex).IsEqualTo(1);
    }

    [Test]
    public async Task InlineTransformWithoutRefreshOptionKeepsDestinationIdentity()
    {
        using var source = new SourceCache<Item, int>(static item => item.Key);
        using var results = source.Connect().TransformWithInlineUpdate(static item => new Destination(item.Value), static (destination, item) => destination.Value = item.Value).AsAggregator();
        source.AddOrUpdate(new Item(1, 10));
        var destination = results.Data.Lookup(1).Value;
        source.AddOrUpdate(new Item(1, 20));
        source.Refresh(source.Lookup(1).Value);

        await Assert.That(results.Data.Lookup(1).Value).IsSameReferenceAs(destination);
        await Assert.That(destination.Value).IsEqualTo(20);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[2].Refreshes).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InlineTransformReportsFactoryAndUpdateErrorsWhileHealthyItemsContinue(bool explicitRefreshOption)
    {
        using var source = new SourceCache<Item, int>(static item => item.Key);
        var errors = new List<Error<Item, int>>();
        Func<Item, Destination> factory = item => item.Value < 0 ? throw new InvalidOperationException("factory") : new Destination(item.Value);
        Action<Destination, Item> update = (destination, item) =>
        {
            if (item.Value < 0)
            {
                throw new InvalidOperationException("update");
            }

            destination.Value = item.Value;
        };
        var stream = explicitRefreshOption
            ? source.Connect().TransformWithInlineUpdate(factory, update, errors.Add, true)
            : source.Connect().TransformWithInlineUpdate(factory, update, errors.Add);
        using var results = stream.AsAggregator();
        source.AddOrUpdate(new Item(1, -1));
        source.AddOrUpdate(new Item(2, 20));
        var destination = results.Data.Lookup(2).Value;
        source.AddOrUpdate(new Item(2, -2));
        source.AddOrUpdate(new Item(2, 30));
        source.RemoveKey(1);

        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors.Select(static error => error.Key)).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Lookup(2).Value).IsSameReferenceAs(destination);
        await Assert.That(destination.Value).IsEqualTo(30);
    }

    private static async Task CheckInitialEmpty<T>(IObservable<T> stream, ReactiveUI.Primitives.Signals.Signal<T> source, T payload, Func<T, int> count)
        where T : class
    {
        var messages = new List<T>();
        var completed = false;
        using var subscription = stream.Subscribe(messages.Add, () => completed = true);
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(count(messages[0])).IsEqualTo(0);

        source.OnNext(payload);
        source.OnCompleted();

        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[1]).IsSameReferenceAs(payload);
        await Assert.That(completed).IsTrue();
    }

    private sealed record Item(int Key, int Value);

    private sealed class Destination(int value)
    {
        public int Value { get; set; } = value;
    }
}
