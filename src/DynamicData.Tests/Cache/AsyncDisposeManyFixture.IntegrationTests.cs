using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public static partial class AsyncDisposeManyFixture
{
    public class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Theory(Timeout = 5_000)]
        [InlineData(ItemType.Disposable)]
        [InlineData(ItemType.AsyncDisposable)]
        [InlineData(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemDisposalErrors_ErrorPropagatesToDisposalsCompleted(ItemType itemType)
        {
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);
            using var sourceCompletionSource = new Subject<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
                .AsyncDisposeMany(disposalsCompleted => 
                {
                    disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");


            source.AddOrUpdate(new[]
            {
                ItemBase.Create(type: itemType, id: 1, version: 1),
                ItemBase.Create(type: itemType, id: 2, version: 1),
                ItemBase.Create(type: itemType, id: 3, version: 1)
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added");
            results.HasCompleted.Should().BeFalse();

            disposalsCompletedResults.Error.Should().BeNull();
            disposalsCompletedResults.RecordedValues.Should().BeEmpty("no disposals should have occurred");
            disposalsCompletedResults.HasCompleted.Should().BeFalse("no disposals should have occurred");


            var error = new Exception("Test");
            source.Items.ElementAt(1).FailDisposal(error);

            sourceCompletionSource.OnNext(Unit.Default);

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized;

            results.Error.Should().BeNull("disposal errors should be propagated on disposalsCompleted");
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed");
            results.HasCompleted.Should().BeTrue();

            disposalsCompletedResults.Error.Should().Be(error, "disposal errors should be caught and propagated on disposalsCompleted");
        }

        [Theory(Timeout = 5_000)]
        [InlineData(ItemType.Plain)]
        [InlineData(ItemType.Disposable)]
        [InlineData(ItemType.AsyncDisposable)]
        [InlineData(ItemType.ImmediateAsyncDisposable)]
        public async Task ItemDisposalsComplete_DisposalsCompletedOccursAndCompletes(ItemType itemType)
        {
            using var source = new SourceCache<ItemBase, int>(static item => item.Id);
            using var sourceCompletionSource = new Subject<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
                .AsyncDisposeMany(disposalsCompleted => 
                {
                    disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");


            source.AddOrUpdate(new[]
            {
                ItemBase.Create(type: itemType, id: 1, version: 1),
                ItemBase.Create(type: itemType, id: 2, version: 1),
                ItemBase.Create(type: itemType, id: 3, version: 1)
            });

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added");
            results.HasCompleted.Should().BeFalse();

            disposalsCompletedResults.Error.Should().BeNull();
            disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
            disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");


            sourceCompletionSource.OnNext(Unit.Default);
            foreach (var item in source.Items)
                item.CompleteDisposal();

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized;

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed");
            results.HasCompleted.Should().BeTrue();

            disposalsCompletedResults.Error.Should().BeNull();
            disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "the source and all disposals have completed");
            disposalsCompletedResults.HasCompleted.Should().BeTrue("the source and all disposals have completed");
        }

        [Fact(Timeout = 5_000)]
        public async Task ItemDisposalsOccurOnMultipleThreads_DisposalIsThreadSafe()
        {
            using var source = new SourceCache<AsyncDisposableItem, int>(static item => item.Id);
            using var sourceCompletionSource = new Subject<Unit>();

            ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

            using var subscription = source
                .Connect()
                .TakeUntil(sourceCompletionSource)
                .AsyncDisposeMany(disposalsCompleted => 
                {
                    disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                    disposalsCompleted.RecordValues(out disposalsCompletedResults);
                })
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");


            var items = Enumerable.Range(1, 100_000)
                .Select(id => new AsyncDisposableItem()
                {
                    Id      = id,
                    Version = 1
                })
                .ToArray();

            source.AddOrUpdate(items);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "items were added");
            results.HasCompleted.Should().BeFalse();

            disposalsCompletedResults.Error.Should().BeNull();
            disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
            disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");


            sourceCompletionSource.OnNext();
            await Task.WhenAll(items
                .GroupBy(item => item.Id % 4)
                .Select(group => Task.Run(() =>
                {
                    foreach (var item in group)
                        item.CompleteDisposal();
                })));

            // RX and TPL don't guarantee Task continuations run synchronously with antecedent completion
            await disposalsCompletedResults.WhenFinalized;

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "no items were removed");
            results.HasCompleted.Should().BeTrue();

            items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed upon source completion");

            disposalsCompletedResults.Error.Should().BeNull();
            disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "the source and all disposals have completed");
            disposalsCompletedResults.HasCompleted.Should().BeTrue("the source and all disposals have completed");
        }
    }
}
