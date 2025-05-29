using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

using FluentAssertions;
using FluentAssertions.Equivalency.Steps;

using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.Cache;

public class AsyncDisposeManyFixture
{
    public enum SourceType
    {
        Subject,
        Immediate
    }

    public enum ItemType
    {
        Plain,
        Disposable,
        AsyncDisposable,
        ImmediateAsyncDisposable
    }

    [Theory]
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
        await disposalsCompletedResults.WaitForFinalizationAsync(TimeSpan.FromSeconds(5));

        results.Error.Should().BeNull("disposal errors should be propagated on disposalsCompleted");
        results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed");
        results.HasCompleted.Should().BeTrue();

        disposalsCompletedResults.Error.Should().Be(error, "disposal errors should be caught and propagated on disposalsCompleted");
    }

    [Theory]
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
        await disposalsCompletedResults.WaitForFinalizationAsync(TimeSpan.FromSeconds(5));

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no items were changed");
        results.HasCompleted.Should().BeTrue();

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "the source and all disposals have completed");
        disposalsCompletedResults.HasCompleted.Should().BeTrue("the source and all disposals have completed");
    }

    [Fact]
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
        await disposalsCompletedResults.WaitForFinalizationAsync(TimeSpan.FromSeconds(30));

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "no items were removed");
        results.HasCompleted.Should().BeTrue();

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed upon source completion");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "the source and all disposals have completed");
        disposalsCompletedResults.HasCompleted.Should().BeTrue("the source and all disposals have completed");
    }

    [Theory]
    [InlineData(ItemType.Plain)]
    [InlineData(ItemType.Disposable)]
    [InlineData(ItemType.AsyncDisposable)]
    [InlineData(ItemType.ImmediateAsyncDisposable)]
    public void ItemsAreAddedMovedOrRefreshed_ItemsAreNotDisposed(ItemType itemType)
    {
        using var source = new Subject<IChangeSet<ItemBase, int>>();

        ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

        using var subscription = source
            .AsyncDisposeMany(disposalsCompleted => 
            {
                disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                disposalsCompleted.RecordValues(out disposalsCompletedResults);
            })
            .ValidateSynchronization()
            .ValidateChangeSets(static item => item.Id)
            .RecordCacheItems(out var results);

        disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");

        
        // Addition
        var items = new List<ItemBase>()
        {
            ItemBase.Create(type: itemType, id: 1, version: 1),
            ItemBase.Create(type: itemType, id: 2, version: 1),
            ItemBase.Create(type: itemType, id: 3, version: 1)
        };

        source.OnNext(new ChangeSet<ItemBase, int>(items
            .Select((item, index) => new Change<ItemBase, int>(
                reason:     ChangeReason.Add,
                key:        item.Id,
                current:    item,
                index:      index))));

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
        results.RecordedItemsSorted.Should().BeEquivalentTo(
            items,
            options => options.WithStrictOrdering(),
            "3 items were added");
        results.HasCompleted.Should().BeFalse();

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeFalse(), "items should not be disposed upon add");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
        disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");


        // Movement
        items.Move(2, 0, items[2]);
        items.Move(2, 1, items[2]);
        source.OnNext(new ChangeSet<ItemBase, int>()
        {
            new(reason: ChangeReason.Moved, key: items[0].Id, current: items[0], previous: Optional.None<ItemBase>(), currentIndex: 0, previousIndex: 2),
            new(reason: ChangeReason.Moved, key: items[1].Id, current: items[1], previous: Optional.None<ItemBase>(), currentIndex: 1, previousIndex: 2)
        });

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
        results.RecordedItemsSorted.Should().BeEquivalentTo(
            items,
            options => options.WithStrictOrdering(),
            "3 items were added");
        results.HasCompleted.Should().BeFalse();

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeFalse(), "items should not be disposed upon movement");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
        disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");


        // Refreshing
        source.OnNext(new ChangeSet<ItemBase, int>(items
            .Select((item, index) => new Change<ItemBase, int>(
                reason:     ChangeReason.Refresh,
                key:        item.Id,
                current:    item,
                index:      index))));

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
        results.RecordedItemsSorted.Should().BeEquivalentTo(
            items,
            options => options.WithStrictOrdering(),
            "3 items were added");
        results.HasCompleted.Should().BeFalse();

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeFalse(), "items should not be disposed upon refresh");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
        disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");
    }

    [Theory]
    [InlineData(ItemType.Plain)]
    [InlineData(ItemType.Disposable)]
    [InlineData(ItemType.AsyncDisposable)]
    [InlineData(ItemType.ImmediateAsyncDisposable)]
    public void ItemsAreRemoved_ItemsAreDisposedAfterDownstreamProcessing(ItemType itemType)
    {
        using var source = new SourceCache<ItemBase, int>(static item => item.Id);

        ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

        using var subscription = source
            .Connect()
            .AsyncDisposeMany(disposalsCompleted => 
            {
                disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                disposalsCompleted.RecordValues(out disposalsCompletedResults);
            })
            .ValidateSynchronization()
            .Do(changes =>
            {
                foreach (var change in changes)
                    if (change.Reason is ChangeReason.Remove)
                        change.Current.HasBeenDisposed.Should().BeFalse("disposal should only occur after downstream processing has completed");
            })
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


        var items = source.Items.ToArray();
        source.Clear();

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
        results.RecordedItemsByKey.Values.Should().BeEmpty("all items were removed");
        results.HasCompleted.Should().BeFalse();

        items.Where(static item => item.CanBeDisposed).Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed after removal");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
        disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");
    }

    [Theory]
    [InlineData(ItemType.Plain)]
    [InlineData(ItemType.Disposable)]
    [InlineData(ItemType.AsyncDisposable)]
    [InlineData(ItemType.ImmediateAsyncDisposable)]
    public void ItemsAreUpdated_PreviousItemsAreDisposedAfterDownstreamProcessing(ItemType itemType)
    {
        using var source = new SourceCache<ItemBase, int>(static item => item.Id);

        ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

        using var subscription = source
            .Connect()
            .AsyncDisposeMany(disposalsCompleted => 
            {
                disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                disposalsCompleted.RecordValues(out disposalsCompletedResults);
            })
            .ValidateSynchronization()
            .Do(changes =>
            {
                foreach (var change in changes)
                    if (change.Reason is ChangeReason.Remove)
                        change.Current.HasBeenDisposed.Should().BeFalse("disposal should only occur after downstream processing has completed");
            })
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


        var previousItems = source.Items.ToArray();
        source.AddOrUpdate(new[]
        {
            ItemBase.Create(type: itemType, id: 1, version: 2),
            ItemBase.Create(type: itemType, id: 2, version: 2),
            ItemBase.Create(type: itemType, id: 3, version: 2)
        });

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "all items were replaced");
        results.HasCompleted.Should().BeFalse();

        previousItems.Where(static item => item.CanBeDisposed).Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed after replacement");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Should().BeEmpty("the source has not completed");
        disposalsCompletedResults.HasCompleted.Should().BeFalse("the source has not completed");
    }

    [Fact]
    public void OnDisposalsCompletedIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.AsyncDisposeMany(
                source:                     Observable.Empty<IChangeSet<ItemBase, int>>(),
                disposalsCompletedAccessor: null!))
            .Should()
            .Throw<ArgumentNullException>();

    [Theory]
    [InlineData(SourceType.Subject)]
    [InlineData(SourceType.Immediate)]
    public void SourceCompletes_ItemsAreDisposedAndCompletionPropagates(SourceType sourceType)
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
            : new Subject<IChangeSet<ImmediateAsyncDisposableItem, int>>();


        ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

        using var subscription = source
            .AsyncDisposeMany(disposalsCompleted => 
            {
                disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                disposalsCompleted.RecordValues(out disposalsCompletedResults);
            })
            .ValidateSynchronization()
            .ValidateChangeSets(static item => item.Id)
            .RecordCacheItems(out var results);

        disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");


        if (source is Subject<IChangeSet<ImmediateAsyncDisposableItem, int>> subject)
        {
            subject.OnNext(changeSet);
            subject.OnCompleted();
        }

        results.Error.Should().BeNull();
        results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "3 items were added");
        results.HasCompleted.Should().BeTrue();

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed upon source completion");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "all items have completed disposal");
        disposalsCompletedResults.HasCompleted.Should().BeTrue("all items have completed disposal");
    }

    [Theory]
    [InlineData(SourceType.Subject)]
    [InlineData(SourceType.Immediate)]
    public void SourceErrors_ItemsAreDisposedAndErrorPropagates(SourceType sourceType)
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
            : new Subject<IChangeSet<ImmediateAsyncDisposableItem, int>>();


        ValueRecordingObserver<Unit>? disposalsCompletedResults = null;

        using var subscription = source
            .AsyncDisposeMany(disposalsCompleted => 
            {
                disposalsCompletedResults.Should().BeNull("disposalsCompletedAccessor should only be invoked once per subscription");
                disposalsCompleted.RecordValues(out disposalsCompletedResults);
            })
            .ValidateSynchronization()
            .ValidateChangeSets(static item => item.Id)
            .RecordCacheItems(out var results);

        disposalsCompletedResults.Should().NotBeNull("disposalsCompletedAccessor should have been invoked");


        if (source is Subject<IChangeSet<ImmediateAsyncDisposableItem, int>> subject)
        {
            subject.OnNext(changeSet);
            subject.OnError(error);
        }

        results.Error.Should().Be(error);
        results.RecordedChangeSets.Count.Should().Be(1, "1 source operation was performed");
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "3 items were added");

        items.Should().AllSatisfy(item => item.HasBeenDisposed.Should().BeTrue(), "disposable items should be disposed upon source failure");

        disposalsCompletedResults.Error.Should().BeNull();
        disposalsCompletedResults.RecordedValues.Count.Should().Be(1, "all items have completed disposal");
        disposalsCompletedResults.HasCompleted.Should().BeTrue("all items have completed disposal");
    }

    [Fact]
    public void SourceIsNull_ThrowsException()
        => FluentActions.Invoking(() => ObservableCacheEx.AsyncDisposeMany<ItemBase, int>(
                source:                     null!,
                disposalsCompletedAccessor: static _ => { }))
            .Should()
            .Throw<ArgumentNullException>();

    public abstract record ItemBase
    {
        public static ItemBase Create(
                ItemType    type,
                int         id,
                int         version)
            => type switch
            {
                ItemType.Plain                      => new PlainItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.Disposable                 => new DisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.AsyncDisposable            => new AsyncDisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                ItemType.ImmediateAsyncDisposable   => new ImmediateAsyncDisposableItem()
                {
                    Id      = id,
                    Version = version
                },
                _                                   => throw new ArgumentException($"{type} is not a valid {nameof(ItemType)} value", nameof(type))
            };

        public required int Id { get; init; }

        public required int Version { get; init; }

        public abstract bool CanBeDisposed { get; }

        public abstract bool HasBeenDisposed { get; }

        public abstract void CompleteDisposal();

        public abstract void FailDisposal(Exception error);
    }

    public sealed record PlainItem
        : ItemBase
    {
        public override bool CanBeDisposed
            => false;

        public override bool HasBeenDisposed
            => false;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error) { }
    }

    public sealed record DisposableItem
        : ItemBase, IDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error)
            => _disposeError = error;

        public void Dispose()
        {
            if (_disposeError is not null)
                #pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                throw _disposeError;
                #pragma warning restore CA1065 // Do not raise exceptions in unexpected locations

            _hasBeenDisposed = true;
        }

        private Exception?  _disposeError;
        private bool        _hasBeenDisposed;
    }

    public sealed record AsyncDisposableItem
        : ItemBase, IAsyncDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal()
            => _disposeCompletionSource.SetResult();

        public override void FailDisposal(Exception error)
            => _disposeCompletionSource.SetException(error);

        public ValueTask DisposeAsync()
        {
            _hasBeenDisposed = true;

            return new(_disposeCompletionSource.Task);
        }

        private readonly TaskCompletionSource _disposeCompletionSource
            = new();
     
        private bool _hasBeenDisposed;
    }

    public sealed record ImmediateAsyncDisposableItem
        : ItemBase, IAsyncDisposable
    {
        public override bool CanBeDisposed
            => true;

        public override bool HasBeenDisposed
            => _hasBeenDisposed;

        public override void CompleteDisposal() { }

        public override void FailDisposal(Exception error)
            => _disposeError = error;

        public ValueTask DisposeAsync()
        {
            _hasBeenDisposed = true;

            return (_disposeError is not null)
                ? ValueTask.FromException(_disposeError)
                : ValueTask.CompletedTask;
        }

        private Exception?  _disposeError;
        private bool        _hasBeenDisposed;
    }
}
