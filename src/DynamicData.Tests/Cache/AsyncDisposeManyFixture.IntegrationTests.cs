#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Cache;

public static partial class AsyncDisposeManyFixture
{
    public class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Test, Timeout(5_000)]
        [Arguments(ItemType.Disposable)]
        [Arguments(ItemType.AsyncDisposable)]
        [Arguments(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemDisposalErrors_ErrorPropagatesToDisposalsCompleted(ItemType itemType, CancellationToken cancellationToken)
        {
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);
            using var sourceCompletionSource = new ReactiveUI.Primitives.Signals.Signal<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
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
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("no disposals should have occurred");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("no disposals should have occurred");

            var error = new Exception("Test");
            source.Items.ElementAt(1).FailDisposal(error);

            sourceCompletionSource.OnNext(Unit.Default);

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized.WaitAsync(cancellationToken);

            await Assert.That(results.Error).IsNull().Because("disposal errors should be propagated on disposalsCompleted");
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items were changed");
            await Assert.That(results.HasCompleted).IsTrue();

            await Assert.That(disposalsCompletedResults.Error).IsEqualTo(error).Because("disposal errors should be caught and propagated on disposalsCompleted");
        }

        [Test, Timeout(5_000)]
        [Arguments(ItemType.Plain)]
        [Arguments(ItemType.Disposable)]
        [Arguments(ItemType.AsyncDisposable)]
        [Arguments(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemDisposalsComplete_DisposalsCompletedOccursAndCompletes(ItemType itemType, CancellationToken cancellationToken)
        {
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);
            using var sourceCompletionSource = new ReactiveUI.Primitives.Signals.Signal<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
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

            sourceCompletionSource.OnNext(Unit.Default);
            foreach (var item in source.Items)
                item.CompleteDisposal();

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized.WaitAsync(cancellationToken);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no items were changed");
            await Assert.That(results.HasCompleted).IsTrue();

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues.Count).IsEqualTo(1).Because("the source and all disposals have completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsTrue().Because("the source and all disposals have completed");
        }

        [Test, Timeout(30_000)]
        public async Task ItemDisposalsOccurOnMultipleThreads_DisposalIsThreadSafe(CancellationToken cancellationToken)
        {
            using var source = new SourceCache<AsyncDisposableItem, int>(static item => item.Id);
            using var sourceCompletionSource = new ReactiveUI.Primitives.Signals.Signal<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;
            var disposalsCompletedAccessorInvokedMoreThanOnce = false;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
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

            var items = Enumerable.Range(1, 100_000)
                .Select(id => new AsyncDisposableItem()
                {
                    Id = id,
                    Version = 1
                })
                .ToArray();

            source.AddOrUpdate(items);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Count).IsEqualTo(source.Count);
            await Assert.That(source.Items.All(item => results.RecordedItemsByKey.TryGetValue(item.Id, out var actual) && ReferenceEquals(actual, item))).IsTrue().Because("each added item should retain its identity");
            await Assert.That(results.HasCompleted).IsFalse();

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues).IsEmpty().Because("the source has not completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsFalse().Because("the source has not completed");

            sourceCompletionSource.OnNext();
            await Task.WhenAll(items
                .GroupBy(item => item.Id % 4)
                .Select(group => Task.Run(() =>
                {
                    foreach (var item in group)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        item.CompleteDisposal();
                    }
                }, cancellationToken)));

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized.WaitAsync(cancellationToken);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("1 source operation was performed");
            await Assert.That(results.RecordedItemsByKey.Count).IsEqualTo(items.Length);
            await Assert.That(items.All(item => results.RecordedItemsByKey.TryGetValue(item.Id, out var actual) && ReferenceEquals(actual, item))).IsTrue().Because("no items were removed or replaced");
            await Assert.That(results.HasCompleted).IsTrue();

            foreach (var item in items) { await Assert.That(item.HasBeenDisposed).IsTrue().Because("disposable items should be disposed upon source completion"); }

            await Assert.That(disposalsCompletedResults.Error).IsNull();
            await Assert.That(disposalsCompletedResults.RecordedValues.Count).IsEqualTo(1).Because("the source and all disposals have completed");
            await Assert.That(disposalsCompletedResults.HasCompleted).IsTrue().Because("the source and all disposals have completed");
        }
    }
}
