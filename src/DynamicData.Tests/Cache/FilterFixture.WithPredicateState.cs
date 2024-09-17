using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using Bogus;
using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public partial class FilterFixture
{
    public sealed class WithPredicateState
    {
        [Fact]
        public void ChangesAreMadeAfterInitialPredicateState_ItemsAreFiltered()
        {
            using var source            = new SourceCache<Item, int>(static item => item.Id);
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            // Set initial state
            predicateState.OnNext(new());

            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test Add changes
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Refresh changes, with no item mutations.
            source.Refresh();

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            results.RecordedChangeSets.Skip(1).First().Select(static change => change.Reason).Should().AllBeEquivalentTo(ChangeReason.Refresh, "all included items should have been refreshed");
            results.RecordedChangeSets.Skip(1).First().Select(static change => change.Current).Should().BeEquivalentTo(EnumerateFilteredItems(), "all included items should have been refreshed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Refresh changes, with item mutations affecting filtering.
            foreach (var item in source.Items)
                item.IsIncluded = !item.IsIncluded;
            source.Refresh();

            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Remove changes
            source.RemoveKeys(new[] { 2, 3 });

            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Update changes, not affecting filtering
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = true }
            });

            results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Update changes, affecting filtering
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Skip(5).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            IEnumerable<Item> EnumerateFilteredItems()
                => source.Items.Where(static item => item.IsIncluded);
        }

        [Fact]
        public void ChangesAreMadeAfterMultiplePredicateStateChanges_ItemsAreFilteredWithLatestPredicateState()
        {
            using var source            = new SourceCache<Item, int>(static item => item.Id);
            using var predicateState    = new BehaviorSubject<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.Id == predicateState)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            // Publish multiple state changes
            predicateState.OnNext(2);
            predicateState.OnNext(3);

            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test filtering of items, by state
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, source.Items.Where(item => item.Id == predicateState.Value));
        }

        [Fact]
        public void ChangesAreMadeBeforeInitialPredicateState_ItemsAreFilteredOnPredicateState()
        {
            using var source            = new SourceCache<Item, int>(static item => item.Id);
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test Add changes
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            // Test Refresh changes, with no item mutations.
            source.Refresh();

            // Test Refresh changes, with item mutations affecting filtering.
            foreach (var item in source.Items)
                item.IsIncluded = !item.IsIncluded;
            source.Refresh();

            // Test Remove changes
            source.RemoveKeys(new[] { 2, 3 });

            // Test Update changes, not affecting filtering
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = true }
            });

            // Test Update changes, affecting filtering
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Should().BeEmpty("the predicate state has not initialized");
            results.RecordedItemsByKey.Should().BeEmpty("the predicate state has not initialized");


            // Set initial state
            predicateState.OnNext(new());

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, source.Items.Where(static item => item.IsIncluded));
        }

        [Fact]
        public void ItemsAreMoved_ChangesAreIgnored()
        {
            using var source            = new Subject<IChangeSet<Item, int>>();
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            var items = new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            };


            // Set initial state
            predicateState.OnNext(new());
            source.OnNext(new ChangeSet<Item, int>(items
                .Select((item, index) => new Change<Item, int>(
                    reason:     ChangeReason.Add,
                    key:        item.Id,
                    current:    item,
                    index:      index))));

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Moved changes, for both included and excluded items.
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Moved, key: 1, current: items[0], previous: default, previousIndex: 0, currentIndex: 1),
                new(reason: ChangeReason.Moved, key: 2, current: items[1], previous: default, previousIndex: 0, currentIndex: 2),
                new(reason: ChangeReason.Moved, key: 3, current: items[2], previous: default, previousIndex: 1, currentIndex: 0)
            });

            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the move operation should have been ignored");
            ShouldBeValid(results, EnumerateFilteredItems());


            IEnumerable<Item> EnumerateFilteredItems()
                => items.Where(static item => item.IsIncluded);
        }

        [Fact]
        public void PredicateIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => Observable.Empty<IChangeSet<Item, int>>()
                    .Filter(
                        predicateState: Observable.Empty<object>(),
                        predicate:      null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public void PredicateStateChanges_ItemsAreRefiltered()
        {
            using var source            = new SourceCache<Item, int>(static item => item.Id);
            using var predicateState    = new BehaviorSubject<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.Id == predicateState)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            // Test filtering of items, by state
            source.AddOrUpdate(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Publish a state change, to change the filtering
            predicateState.OnNext(2);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, EnumerateFilteredItems());


            IEnumerable<Item> EnumerateFilteredItems()
                => source.Items.Where(item => item.Id == predicateState.Value);
        }

        [Fact]
        public void PredicateStateCompletesAfterInitialValue_CompletionWaitsForSourceCompletion()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Return(new object()),
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            results.HasCompleted.Should().BeFalse("changes could still be generated by the source");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.OnCompleted();

            results.HasCompleted.Should().BeTrue("all input streams have completed");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
        }

        [Fact]
        public void PredicateStateCompletesImmediately_CompletionIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Empty<object>(),
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            results.HasCompleted.Should().BeTrue("completion of the predicate state stream before it emits any values means that items can never be accepted by the filter predicate");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Fact]
        public void PredicateStateErrors_ErrorIsPropagated()
        {
            using var source            = new Subject<IChangeSet<Item, int>>();
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            var error = new Exception("This is a test.");
            predicateState.OnError(error);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void PredicateStateErrorsImmediately_ErrorIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            var error = new Exception("This is a test.");

            using var subscription = source
                .Filter(
                    predicateState: Observable.Throw<object>(error),
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Fact]
        public void PredicateStateIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => Observable.Empty<IChangeSet<Item, int>>()
                    .Filter(
                        predicateState: (null as IObservable<object>)!,
                        predicate:      static (_, _) => true))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public async Task SourceAndPredicateStateNotifyFromDifferentThreads_FilteringIsThreadSafe()
        {
            var randomizer = new Randomizer(0x1234567);

            (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                editCount:      5_000,
                maxChangeCount: 20,
                randomizer:     randomizer);
            
            var predicateStates = GenerateRandomPredicateStates(
                valueCount: 5_000,
                randomizer: randomizer);


            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<int>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      Item.FilterByIdInclusionMask)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await Task.WhenAll(
                Task.Run(
                    action:             () =>
                    {
                        foreach (var changeSet in changeSets)
                            source.OnNext(changeSet);
                    },
                    cancellationToken:  timeoutSource.Token),
                Task.Run(
                    action:             () =>
                    {
                        foreach (var value in predicateStates)
                            predicateState.OnNext(value);
                    },
                    cancellationToken:  timeoutSource.Token));

            var finalPredicateState = predicateStates[^1];
            ShouldBeValid(results, items.Items.Where(item => Item.FilterByIdInclusionMask(finalPredicateState, item)));
        }

        [Fact]
        public void SourceCompletesWhenEmpty_CompletionIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            source.OnCompleted();

            results.HasCompleted.Should().BeTrue("no further changes can occur when there are no items to be filtered or unfiltered");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void SourceCompletesWhenNotEmpty_CompletionWaitsForStateCompletion()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            source.OnNext(new ChangeSet<Item, int>() { new(reason: ChangeReason.Add, key: 1, current: new Item() { Id = 1, IsIncluded = true }) });
            source.OnCompleted();

            results.HasCompleted.Should().BeFalse("changes could still be generated by changes in predicate state");
            results.RecordedChangeSets.Should().BeEmpty("the predicate has not initialized");

            predicateState.OnCompleted();

            results.HasCompleted.Should().BeTrue("all input streams have completed");
            results.RecordedChangeSets.Should().BeEmpty("the predicate never initialized");
        }

        [Fact]
        public void SourceCompletesImmediately_CompletionIsPropagated()
        {
            using var predicateState = new Subject<object>();

            using var subscription = Observable.Empty<IChangeSet<Item, int>>()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            results.HasCompleted.Should().BeTrue("no further changes can occur when there are no items to be filtered or unfiltered");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void SourceErrors_ErrorIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            var error = new Exception("This is a test.");
            source.OnError(error);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void SourceErrorsImmediately_ErrorIsPropagated()
        {
            using var predicateState = new Subject<object>();

            var error = new Exception("This is a test.");

            using var subscription = Observable.Throw<IChangeSet<Item, int>>(error)
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void SourceIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => ObservableCacheEx.Filter(
                    source:         (null as IObservable<IChangeSet<Item, int>>)!,
                    predicateState: Observable.Empty<object>(),
                    predicate:      static (_, _) => true))
                .Should()
                .Throw<ArgumentNullException>();

        [Fact]
        public void SubscriptionIsDisposed_UnsubscriptionIsPropagated()
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            subscription.Dispose();

            source.HasObservers.Should().BeFalse("subscription disposal should be propagated to all input streams");
            predicateState.HasObservers.Should().BeFalse("subscription disposal should be propagated to all input streams");

            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
        }

        [Theory]
        [InlineData("source", "predicateState")]
        [InlineData("predicateState", "source")]
        public void SuppressEmptyChangeSetsIsFalse_EmptyChangesetsArePropagatedAndOnlyFinalCompletionIsPropagated(params string[] completionOrder)
        {
            using var source = new Subject<IChangeSet<Item, int>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState:             predicateState,
                    predicate:                  static (predicateState, item) => item.IsIncluded,
                    suppressEmptyChangeSets:    false)
                .ValidateSynchronization()
                .ValidateChangeSets(static item => item.Id)
                .RecordCacheItems(out var results);


            // Initialize the predicate
            predicateState.OnNext(new object());

            results.RecordedChangeSets.Count.Should().Be(1, "the predicate state was initialized");
            results.RecordedChangeSets[0].Should().BeEmpty("there are no items in the collection");
            ShouldBeValid(results, Enumerable.Empty<Item>());


            // Publish an empty changeset
            source.OnNext(ChangeSet<Item, int>.Empty);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "a source operation was performed");
            results.RecordedChangeSets.Skip(1).First().Should().BeEmpty("the source changeset was empty");
            ShouldBeValid(results, Enumerable.Empty<Item>());


            // Publish a changeset with only excluded items
            source.OnNext(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Add, key: 1, current: new Item() { Id = 1, IsIncluded = false }),
                new(reason: ChangeReason.Add, key: 2, current: new Item() { Id = 2, IsIncluded = false }),
                new(reason: ChangeReason.Add, key: 3, current: new Item() { Id = 3, IsIncluded = false })
            });

            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "a source operation was performed");
            results.RecordedChangeSets.Skip(2).First().Should().BeEmpty("all source items were excluded");
            ShouldBeValid(results, Enumerable.Empty<Item>());

            for (var i = 0; i < completionOrder.Length; ++i)
            {
                switch (completionOrder[i])
                {
                    case nameof(source):
                        source.OnCompleted();
                        break;

                    case nameof(predicateState):
                        predicateState.OnCompleted();
                        break;
                }

                if (i < (completionOrder.Length - 1))
                    results.HasCompleted.Should().BeFalse("not all input streams have completed");
            }

            results.HasCompleted.Should().BeTrue("all input streams have completed");
        }

        private static void ShouldBeValid(
            CacheItemRecordingObserver<Item, int>   results,
            IEnumerable<Item>                       expectedFilteredItems)
        {
            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("no completion events should have occurred");
            results.RecordedChangeSets.Should().AllSatisfy(changeSet =>
            {
                if (changeSet.Count is not 0)
                    changeSet.Should().AllSatisfy(change =>
                    {
                        change.CurrentIndex.Should().Be(-1, "sorting indexes should not be propagated");
                        change.PreviousIndex.Should().Be(-1, "sorting indexes should not be propagated");
                    });
            });
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(expectedFilteredItems, "all filtered items should match the filter predicate");
            results.RecordedItemsSorted.Should().BeEmpty("sorting is not supported by filter opreators");
        }

        private static (ICache<Item, int> items, IReadOnlyList<IChangeSet<Item, int>> changeSets) GenerateStressItemsAndChangeSets(
            int         editCount,
            int         maxChangeCount,
            Randomizer  randomizer)
        {
            // Not exercising Moved, since ChangeAwareCache<> doesn't support it, and I'm too lazy to implement it by hand.
            var changeReasons = new[]
            {
                ChangeReason.Add,
                ChangeReason.Refresh,
                ChangeReason.Remove,
                ChangeReason.Update
            };

            // Weights are chosen to make the cache size likely to grow over time,
            // exerting more pressure on the system the longer the benchmark runs.
            // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
            var changeReasonWeightsWhenCountIs0 = new[]
            {
                1f, // Add
                0f, // Refresh
                0f, // Remove
                0f  // Update
            };

            var changeReasonWeightsOtherwise = new[]
            {
                0.30f, // Add
                0.25f, // Refresh
                0.20f, // Remove
                0.25f  // Update
            };

            var nextItemId = 1;

            var changeSets = new List<IChangeSet<Item, int>>(capacity: editCount);

            var items = new ChangeAwareCache<Item, int>();

            while (changeSets.Count < changeSets.Capacity)
            {
                var changeCount = randomizer.Int(1, maxChangeCount);
                for (var i = 0; i < changeCount; ++i)
                {
                    var changeReason = randomizer.WeightedRandom(changeReasons, items.Count switch
                    {
                        0   => changeReasonWeightsWhenCountIs0,
                        _   => changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ChangeReason.Add:
                            items.AddOrUpdate(
                                item:   new Item()
                                {
                                    Id          = nextItemId,
                                    IsIncluded  = randomizer.Bool()
                                },
                                key:    nextItemId);
                            ++nextItemId;
                            break;

                        case ChangeReason.Refresh:
                            items.Refresh(items.Keys.ElementAt(randomizer.Int(0, items.Count - 1)));
                            break;

                        case ChangeReason.Remove:
                            items.Remove(items.Keys.ElementAt(randomizer.Int(0, items.Count - 1)));
                            break;

                        case ChangeReason.Update:
                            var id = items.Keys.ElementAt(randomizer.Int(0, items.Count - 1));
                            items.AddOrUpdate(
                                item:   new Item()
                                {
                                    Id          = id,
                                    IsIncluded  = randomizer.Bool()
                                },
                                key:    id);
                            break;
                    }
                }

                changeSets.Add(items.CaptureChanges());
            }

            return (items, changeSets);
        }

        private static IReadOnlyList<int> GenerateRandomPredicateStates(
            int         valueCount,
            Randomizer  randomizer)
        {
            var values = new List<int>(capacity: valueCount);

            while (values.Count < valueCount)
                values.Add(randomizer.Int());

            return values;
        }

        private class Item
        {
            public static bool FilterByIdInclusionMask(
                    int     idInclusionMask,
                    Item    item)
                => ((item.Id & idInclusionMask) == 0) && item.IsIncluded;
            
            public required int Id { get; init; }

            public bool IsIncluded { get; set; }

            public override string ToString()
                => $"{{ Id = {Id}, IsIncluded = {IsIncluded} }}";
        }
    }
}
