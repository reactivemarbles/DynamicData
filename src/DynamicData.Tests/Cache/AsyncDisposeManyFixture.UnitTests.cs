#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

namespace DynamicData.Tests.Cache;

public static partial class AsyncDisposeManyFixture
{
    public class UnitTests
    {
        [Test]
        [Arguments(ItemType.Plain)]
        [Arguments(ItemType.Disposable)]
        [Arguments(ItemType.AsyncDisposable)]
        [Arguments(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemsAreAddedMovedOrRefreshed_ItemsAreNotDisposed(ItemType itemType)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<ItemBase, int>>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;


            using var subscription = source
                .AsyncDisposeMany(disposalsCompleted =>
                {
                    if (disposalsCompletedResults is not null)
                    {
                        disposalsCompletedAccessorInvokedMoreThanOnce = true;
                    }
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            await Assert.That(disposalsCompletedResults).IsNotNull().Because("disposalsCompletedAccessor should have been invoked");
            await Assert.That(disposalsCompletedAccessorInvokedMoreThanOnce).IsFalse().Because("disposalsCompletedAccessor should only be invoked once per subscription");

            // Addition
            var items = new List<ItemBase>()
            {
                ItemBase.Create(type: itemType, id: 1, version: 1),
                ItemBase.Create(type: itemType, id: 2, version: 1),
                ItemBase.Create(type: itemType, id: 3, version: 1)
            };

            source.OnNext(new ChangeSet<ItemBase, int>(items
                .Select((item, index) => new Change<ItemBase, int>(
                    reason: ChangeReason.Add,
                    key: item.Id,
                    current: item,
                    index: index))));

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsSorted).IsEquivalentTo(items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsFalse();

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsFalse().Because("items should not be disposed upon add"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");

            // Movement
            items.Move(2, 0, items[2]);
            items.Move(2, 1, items[2]);
            source.OnNext(new ChangeSet<ItemBase, int>()
            {
                new(reason: ChangeReason.Moved, key: items[0].Id, current: items[0], previous: ReactiveUI.Primitives.Optional<ItemBase>.None, currentIndex: 0, previousIndex: 2),
                new(reason: ChangeReason.Moved, key: items[1].Id, current: items[1], previous: ReactiveUI.Primitives.Optional<ItemBase>.None, currentIndex: 1, previousIndex: 2)
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsSorted).IsEquivalentTo(items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsFalse();

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsFalse().Because("items should not be disposed upon movement"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");

            // Refreshing
            source.OnNext(new ChangeSet<ItemBase, int>(items
                .Select((item, index) => new Change<ItemBase, int>(
                    reason: ChangeReason.Refresh,
                    key: item.Id,
                    current: item,
                    index: index))));

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsSorted).IsEquivalentTo(items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsFalse();

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsFalse().Because("items should not be disposed upon refresh"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(ItemType.Plain)]
        [Arguments(ItemType.Disposable)]
        [Arguments(ItemType.AsyncDisposable)]
        [Arguments(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemsAreRemoved_ItemsAreDisposedAfterDownstreamProcessing(ItemType itemType)
        {
            var itemsDisposedBeforeDownstreamProcessing = new List<ItemBase>();
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .Connect()
                .AsyncDisposeMany(disposalsCompleted =>
                {
                    if (disposalsCompletedResults is not null)
                    {
                        disposalsCompletedAccessorInvokedMoreThanOnce = true;
                    }

                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .Do(changes =>
                {
                    foreach (var change in changes)
                    {
                        if (change.Reason is ChangeReason.Remove)
                        {
                            if (change.Current.HasBeenDisposed)
                            {
                                itemsDisposedBeforeDownstreamProcessing.Add(change.Current);
                            }
                        }
                    }
                })
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            await Assert.That(disposalsCompletedResults).IsNotNull().Because("disposalsCompletedAccessor should have been invoked");
            await Assert.That(disposalsCompletedAccessorInvokedMoreThanOnce).IsFalse().Because("disposalsCompletedAccessor should only be invoked once per subscription");

            source.AddOrUpdate(new[]
            {
                ItemBase.Create(type: itemType, id: 1, version: 1),
                ItemBase.Create(type: itemType, id: 2, version: 1),
                ItemBase.Create(type: itemType, id: 3, version: 1)
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsFalse();

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");

            var items = source.Items.ToArray();
            source.Clear();

            await Assert.That(results.Error).IsNull();
            await Assert.That(itemsDisposedBeforeDownstreamProcessing).IsEmpty().Because("disposal should only occur after downstream processing has completed");
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEmpty().Because("all items were removed");
            await Assert.That(results.HasCompleted).IsFalse();

            foreach (var item in items.Where(static item => item.CanBeDisposed)) { await Assert.That(item.HasBeenDisposed).IsTrue().Because("disposable items should be disposed after removal"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        [Arguments(ItemType.Plain)]
        [Arguments(ItemType.Disposable)]
        [Arguments(ItemType.AsyncDisposable)]
        [Arguments(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemsAreUpdated_PreviousItemsAreDisposedAfterDownstreamProcessing(ItemType itemType)
        {
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;
            var itemsDisposedBeforeDownstreamProcessing = new List<ItemBase>();

            using var subscription = source
                .Connect()
                .AsyncDisposeMany(disposalsCompleted =>
                {
                    if (disposalsCompletedResults is not null)
                    {
                        disposalsCompletedAccessorInvokedMoreThanOnce = true;
                    }

                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .Do(changes =>
                {
                    foreach (var change in changes)
                    {
                        if (change.Reason is ChangeReason.Remove)
                        {
                            if (change.Current.HasBeenDisposed)
                            {
                                itemsDisposedBeforeDownstreamProcessing.Add(change.Current);
                            }
                        }
                    }
                })
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            await Assert.That(disposalsCompletedResults).IsNotNull().Because("disposalsCompletedAccessor should have been invoked");
            await Assert.That(disposalsCompletedAccessorInvokedMoreThanOnce).IsFalse().Because("disposalsCompletedAccessor should only be invoked once per subscription");

            source.AddOrUpdate(new[]
            {
                ItemBase.Create(type: itemType, id: 1, version: 1),
                ItemBase.Create(type: itemType, id: 2, version: 1),
                ItemBase.Create(type: itemType, id: 3, version: 1)
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsFalse();

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");

            var previousItems = source.Items.ToArray();
            source.AddOrUpdate(new[]
            {
                ItemBase.Create(type: itemType, id: 1, version: 2),
                ItemBase.Create(type: itemType, id: 2, version: 2),
                ItemBase.Create(type: itemType, id: 3, version: 2)
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(itemsDisposedBeforeDownstreamProcessing).IsEmpty().Because("disposal should only occur after downstream processing has completed");
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("all items were replaced");
            await Assert.That(results.HasCompleted).IsFalse();

            foreach (var item in previousItems.Where(static item => item.CanBeDisposed)) { await Assert.That(item.HasBeenDisposed).IsTrue().Because("disposable items should be disposed after replacement"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");
        }

        [Test]
        public async Task OnDisposalsCompletedIsNull_ThrowsException()
            => await Assert.That(() => ObservableCacheEx.AsyncDisposeMany(
                    source: Observable.Empty<IChangeSet<ItemBase, int>>(),
                    disposalsCompletedAccessor: null!)).Throws<ArgumentNullException>();

        [Test]
        [Arguments(SourceType.Subject)]
        [Arguments(SourceType.Immediate)]
        public async Task SourceCompletes_ItemsAreDisposedAndCompletionPropagates(SourceType sourceType)
        {
            var items = new[]
            {
                new ImmediateAsyncDisposableItem() { Id = 1, Version = 1},
                new ImmediateAsyncDisposableItem() { Id = 2, Version = 1},
                new ImmediateAsyncDisposableItem() { Id = 3, Version = 1}
            };

            var changeSet = new ChangeSet<ImmediateAsyncDisposableItem, int>(items
                .Select(item => new Change<ImmediateAsyncDisposableItem, int>(reason: ChangeReason.Add, key: item.Id, current: item)));

            IObservable<IChangeSet<ImmediateAsyncDisposableItem, int>> source = (sourceType is SourceType.Immediate)
                ? Observable.Return(changeSet)
                : new ReactiveUI.Primitives.Signals.Signal<IChangeSet<ImmediateAsyncDisposableItem, int>>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .AsyncDisposeMany(disposalsCompleted =>
                {
                    if (disposalsCompletedResults is not null)
                    {
                        disposalsCompletedAccessorInvokedMoreThanOnce = true;
                    }
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            await Assert.That(disposalsCompletedResults).IsNotNull().Because("disposalsCompletedAccessor should have been invoked");
            await Assert.That(disposalsCompletedAccessorInvokedMoreThanOnce).IsFalse().Because("disposalsCompletedAccessor should only be invoked once per subscription");

            if (source is ReactiveUI.Primitives.Signals.Signal<IChangeSet<ImmediateAsyncDisposableItem, int>> subject)
            {
                subject.OnNext(changeSet);
                subject.OnCompleted();
            }

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("3 items were added");
            await Assert.That(results.HasCompleted).IsTrue();

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsTrue().Because("disposable items should be disposed upon source completion"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues.Count).IsEqualTo(1).Because("all items have completed disposal");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsTrue().Because("all items have completed disposal");
        }

        [Test]
        [Arguments(SourceType.Subject)]
        [Arguments(SourceType.Immediate)]
        public async Task SourceErrors_ItemsAreDisposedAndErrorPropagates(SourceType sourceType)
        {
            var items = new[]
            {
                new ImmediateAsyncDisposableItem() { Id = 1, Version = 1},
                new ImmediateAsyncDisposableItem() { Id = 2, Version = 1},
                new ImmediateAsyncDisposableItem() { Id = 3, Version = 1}
            };

            var changeSet = new ChangeSet<ImmediateAsyncDisposableItem, int>(items
                .Select(item => new Change<ImmediateAsyncDisposableItem, int>(reason: ChangeReason.Add, key: item.Id, current: item)));

            var error = new Exception("Test");

            IObservable<IChangeSet<ImmediateAsyncDisposableItem, int>> source = (sourceType is SourceType.Immediate)
                ? Observable.Return(changeSet)
                    .Concat(Observable.Throw<IChangeSet<ImmediateAsyncDisposableItem, int>>(error))
                : new ReactiveUI.Primitives.Signals.Signal<IChangeSet<ImmediateAsyncDisposableItem, int>>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .AsyncDisposeMany(disposalsCompleted =>
                {
                    if (disposalsCompletedResults is not null)
                    {
                        disposalsCompletedAccessorInvokedMoreThanOnce = true;
                    }
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            await Assert.That(disposalsCompletedResults).IsNotNull().Because("disposalsCompletedAccessor should have been invoked");
            await Assert.That(disposalsCompletedAccessorInvokedMoreThanOnce).IsFalse().Because("disposalsCompletedAccessor should only be invoked once per subscription");

            if (source is ReactiveUI.Primitives.Signals.Signal<IChangeSet<ImmediateAsyncDisposableItem, int>> subject)
            {
                subject.OnNext(changeSet);
                subject.OnError(error);
            }

            await Assert.That(results.Error).IsEqualTo(error);
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items).Because("3 items were added");

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsTrue().Because("disposable items should be disposed upon source failure"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues.Count).IsEqualTo(1).Because("all items have completed disposal");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsTrue().Because("all items have completed disposal");
        }

        [Test]
        public async Task SourceIsNull_ThrowsException()
            => await Assert.That(() => ObservableCacheEx.AsyncDisposeMany<ItemBase, int>(
                    source: null!,
                    disposalsCompletedAccessor: static _ => { })).Throws<ArgumentNullException>();
    }
}
