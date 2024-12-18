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
using Xunit.Abstractions;

namespace DynamicData.Tests.List;

public partial class FilterFixture
{
    public sealed class WithPredicateState
    {
        private readonly ITestOutputHelper _output;

        public WithPredicateState(ITestOutputHelper output)
            => _output = output;

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void ChangesAreMadeAfterInitialPredicateState_ItemsAreFiltered(ListFilterPolicy filterPolicy)
        {
            using var source            = new TestSourceList<Item>();
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            // Set initial state
            predicateState.OnNext(new());

            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test Add, with an included item
            var item1 = new Item() { Id = 1, IsIncluded = true };
            source.Add(item1);

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed, with one included item added");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Add, with an excluded item
            var item2 = new Item() { Id = 2, IsIncluded = false };
            source.Add(item2);

            results.RecordedChangeSets.Skip(1).Should().BeEmpty("one source operation was performed, but no included items were affected");


            // Test AddRange, with both included and excluded items
            var item3 = new Item() { Id = 3, IsIncluded = false };
            var item4 = new Item() { Id = 4, IsIncluded = true };
            var item5 = new Item() { Id = 5, IsIncluded = true };
            var item6 = new Item() { Id = 6, IsIncluded = false };
            var item7 = new Item() { Id = 7, IsIncluded = false };
            var item8 = new Item() { Id = 8, IsIncluded = true };
            source.AddRange(new[] { item3, item4, item5, item6, item7, item8 });

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed, with 3 included items added");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Refresh, with no item mutations.
            source.Refresh(Enumerable.Range(0, source.Count));

            results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "one source operation was performed, with all included items affected");
            results.RecordedChangeSets.Skip(2).First().Select(static change => change.Reason).Should().AllBeEquivalentTo(ListChangeReason.Refresh, "all included items should have been refreshed");
            results.RecordedChangeSets.Skip(2).First().Select(static change => change.Item.Current).Should().BeEquivalentTo(EnumerateFilteredItems(), "all included items should have been refreshed");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Refresh, with item mutations affecting filtering.
            item1.IsIncluded = !item1.IsIncluded;
            item3.IsIncluded = !item3.IsIncluded;
            item5.IsIncluded = !item5.IsIncluded;
            item6.IsIncluded = !item6.IsIncluded;
            source.Refresh(Enumerable.Range(0, source.Count));

            results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "one source operation was performed, with items being included and excluded");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Remove, with an included item
            source.RemoveAt(3);

            results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "one source operation was performed, with one included item affected");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Remove, with an excluded item
            source.RemoveAt(3);

            results.RecordedChangeSets.Skip(5).Should().BeEmpty("one source operation was performed, but no included items were affected");

            
            // Test Remove, with both included and excluded items
            source.RemoveRange(index: 2, count: 2);

            results.RecordedChangeSets.Skip(5).Count().Should().Be(1, "one source operation was performed, with one included item affected");
            ShouldBeValid(results, EnumerateFilteredItems());

            
            // Test Replace, not affecting filtering
            var item9 = new Item() { Id = 9, IsIncluded = false };
            var item10 = new Item() { Id = 10, IsIncluded = true };
            source.Edit(updater =>
            {
                updater.Replace(item7, item9);
                updater.Replace(item8, item10);
            });

            results.RecordedChangeSets.Skip(6).Count().Should().Be(1, "one source operation was performed, with one included item affected");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Replace, affecting filtering
            var item11 = new Item() { Id = 11, IsIncluded = true };
            var item12 = new Item() { Id = 12, IsIncluded = false };
            source.Edit(updater =>
            {
                updater.Replace(item9, item11);
                updater.Replace(item10, item12);
            });

            results.RecordedChangeSets.Skip(7).Count().Should().Be(1, "one source operation was performed, with one included item affected");
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Move of an included item, relative to another included item
            var item13 = new Item() { Id = 13, IsIncluded = true };
            source.Add(item13);
            source.Move(2, 4);

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    results.RecordedChangeSets.Skip(8).Count().Should().Be(2, "two source operations were performed");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    results.RecordedChangeSets.Skip(8).Count().Should().Be(1, "two source operations were performed, one of which was a move, which are not propagated, as ordering is not preserved");
                    break;
            }
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Move of an excluded item
            source.Move(4, 2);

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    results.RecordedChangeSets.Skip(10).Count().Should().Be(1, "one source operation was performed");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    results.RecordedChangeSets.Skip(9).Should().BeEmpty("one source operation was performed, a move, which are not propagated, as ordering is not preserved");
                    break;
            }


            // Test Clear, with included items
            source.Clear();

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    results.RecordedChangeSets.Skip(11).Count().Should().Be(1, "one source operation was performed, with all included items affected");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    results.RecordedChangeSets.Skip(9).Count().Should().Be(1, "one source operation was performed, with all included items affected");
                    break;
            }
            ShouldBeValid(results, EnumerateFilteredItems());


            // Test Clear, with only excluded items
            source.Add(new Item() { Id = 14, IsIncluded = false });
            source.Clear();

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    results.RecordedChangeSets.Skip(12).Should().BeEmpty("two source operations were performed, and neither affected included items");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    results.RecordedChangeSets.Skip(10).Should().BeEmpty("two source operations were performed, and neither affected included items");
                    break;
            }
            ShouldBeValid(results, EnumerateFilteredItems());


            IEnumerable<Item> EnumerateFilteredItems()
                => source.Items.Where(static item => item.IsIncluded);
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void ChangesAreMadeAfterMultiplePredicateStateChanges_ItemsAreFilteredWithLatestPredicateState(ListFilterPolicy filterPolicy)
        {
            using var source            = new SourceList<Item>();
            using var predicateState    = new BehaviorSubject<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.Id == predicateState,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            // Publish multiple state changes
            predicateState.OnNext(2);
            predicateState.OnNext(3);

            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test filtering of items, by state
            source.AddRange(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, source.Items.Where(item => item.Id == predicateState.Value));
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void ChangesAreMadeBeforeInitialPredicateState_ItemsAreFilteredOnPredicateState(ListFilterPolicy filterPolicy)
        {
            using var source            = new TestSourceList<Item>();
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            results.RecordedChangeSets.Should().BeEmpty("no source operations have been performed");


            // Test Add, with an included item
            var item1 = new Item() { Id = 1, IsIncluded = true };
            source.Add(item1);

            // Test Add, with an excluded item
            var item2 = new Item() { Id = 2, IsIncluded = false };
            source.Add(item2);

            // Test AddRange, with both included and excluded items
            var item3 = new Item() { Id = 3, IsIncluded = false };
            var item4 = new Item() { Id = 4, IsIncluded = true };
            var item5 = new Item() { Id = 5, IsIncluded = true };
            var item6 = new Item() { Id = 6, IsIncluded = false };
            var item7 = new Item() { Id = 7, IsIncluded = false };
            var item8 = new Item() { Id = 8, IsIncluded = true };
            source.AddRange(new[] { item3, item4, item5, item6, item7, item8 });

            // Test Refresh, with no item mutations.
            source.Refresh(Enumerable.Range(0, source.Count));

            // Test Refresh, with item mutations affecting filtering.
            item1.IsIncluded = !item1.IsIncluded;
            item3.IsIncluded = !item3.IsIncluded;
            item5.IsIncluded = !item5.IsIncluded;
            item6.IsIncluded = !item6.IsIncluded;
            source.Refresh(Enumerable.Range(0, source.Count));

            // Test Remove, with an included item
            source.RemoveAt(3);

            // Test Remove, with an excluded item
            source.RemoveAt(3);
            
            // Test Remove, with both included and excluded items
            source.RemoveRange(index: 2, count: 2);
            
            // Test Replace, not affecting filtering
            var item9 = new Item() { Id = 9, IsIncluded = false };
            var item10 = new Item() { Id = 10, IsIncluded = true };
            source.Edit(updater =>
            {
                updater.Replace(item7, item9);
                updater.Replace(item8, item10);
            });

            // Test Replace, affecting filtering
            var item11 = new Item() { Id = 11, IsIncluded = true };
            var item12 = new Item() { Id = 12, IsIncluded = false };
            source.Edit(updater =>
            {
                updater.Replace(item9, item11);
                updater.Replace(item10, item12);
            });

            // Test Move of an included item, relative to another included item
            var item13 = new Item() { Id = 13, IsIncluded = true };
            source.Add(item13);
            source.Move(2, 4);

            // Test Move of an excluded item
            source.Move(4, 2);

            results.RecordedChangeSets.Should().BeEmpty("the predicate state has not initialized");


            // Set initial state
            predicateState.OnNext(new());

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, source.Items.Where(static item => item.IsIncluded));
        }

        [Fact]
        public void FilterPolicyIsClearAndReplace_ReFilteringPreservesOrder()
        {
            using var source            = new SourceList<Item>();
            using var predicateState    = new BehaviorSubject<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.Id == predicateState,
                    filterPolicy:   ListFilterPolicy.ClearAndReplace)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            // Test filtering of items, by state
            source.AddRange(new[]
            {
                new Item() { Id = 1,    IsIncluded = true },
                new Item() { Id = 2,    IsIncluded = true },
                new Item() { Id = 3,    IsIncluded = false },
                new Item() { Id = 4,    IsIncluded = false },
                new Item() { Id = 5,    IsIncluded = true },
                new Item() { Id = 6,    IsIncluded = false },
                new Item() { Id = 7,    IsIncluded = false },
                new Item() { Id = 8,    IsIncluded = true },
                new Item() { Id = 9,    IsIncluded = false },
                new Item() { Id = 10,   IsIncluded = true }
            });

            results.RecordedChangeSets.Count.Should().Be(1, "one source operation was performed");

            // Capture the current set of filtered items, and publish a state change, to force a re-filter
            var priorFilteredItems = results.RecordedItems.ToArray();
            predicateState.OnNext(1);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "one source operation was performed");
            ShouldBeValid(results, source.Items.Where(item => item.Id == predicateState.Value));
            results.RecordedItems.Should().BeEquivalentTo(priorFilteredItems, options => options.WithStrictOrdering());
        }

        [Fact]
        public void PredicateIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => Observable.Empty<IChangeSet<Item>>()
                    .Filter(
                        predicateState: Observable.Empty<object>(),
                        predicate:      null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void PredicateStateChanges_ItemsAreReFiltered(ListFilterPolicy filterPolicy)
        {
            using var source            = new SourceList<Item>();
            using var predicateState    = new BehaviorSubject<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.Id == predicateState,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            // Test filtering of items, by state
            source.AddRange(new[]
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

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void PredicateStateCompletesAfterInitialValue_CompletionWaitsForSourceCompletion(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Return(new object()),
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.HasCompleted.Should().BeFalse("changes could still be generated by the source");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.OnCompleted();

            results.HasCompleted.Should().BeTrue("all input streams have completed");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void PredicateStateCompletesImmediately_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Empty<object>(),
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.HasCompleted.Should().BeTrue("completion of the predicate state stream before it emits any values means that items can never be accepted by the filter predicate");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void PredicateStateErrors_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source            = new Subject<IChangeSet<Item>>();
            using var predicateState    = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            var error = new Exception("This is a test.");
            predicateState.OnError(error);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void PredicateStateErrorsImmediately_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            var error = new Exception("This is a test.");

            using var subscription = source
                .Filter(
                    predicateState: Observable.Throw<object>(error),
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Fact]
        public void PredicateStateIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => Observable.Empty<IChangeSet<Item>>()
                    .Filter(
                        predicateState: (null as IObservable<object>)!,
                        predicate:      static (_, _) => true))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceAndPredicateStateNotifyFromDifferentThreads_FilteringIsThreadSafe(ListFilterPolicy filterPolicy)
        {
            var randomizer = new Randomizer(0x1234567);

            (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                editCount:      5_000,
                maxChangeCount: 20,
                maxRangeSize:   10,
                randomizer:     randomizer);
            
            var predicateStates = GenerateRandomPredicateStates(
                valueCount: 5_000,
                randomizer: randomizer);


            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<int>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      Item.FilterByIdInclusionMask,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            //int i;
            //for (i = 0; (i < changeSets.Count) && (i < predicateStates.Count); ++i)
            //{
            //    source.OnNext(changeSets[i]);
            //    predicateState.OnNext(predicateStates[i]);
            //}

            //for (; i < changeSets.Count; ++i)
            //    source.OnNext(changeSets[i]);

            //for (; i < predicateStates.Count; ++i)
            //    predicateState.OnNext(predicateStates[i]);

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
            ShouldBeValid(results, items.Where(item => Item.FilterByIdInclusionMask(finalPredicateState, item)));
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SourceCompletesWhenEmpty_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            source.OnCompleted();

            results.HasCompleted.Should().BeTrue("no further changes can occur when there are no items to be filtered or unfiltered");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SourceCompletesWhenNotEmpty_CompletionWaitsForStateCompletion(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            source.OnNext(new ChangeSet<Item>() { new(reason: ListChangeReason.Add, current: new Item() { Id = 1, IsIncluded = true }, index: 0) });
            source.OnCompleted();

            results.HasCompleted.Should().BeFalse("changes could still be generated by changes in predicate state");
            results.RecordedChangeSets.Should().BeEmpty("the predicate has not initialized");

            predicateState.OnCompleted();

            results.HasCompleted.Should().BeTrue("all input streams have completed");
            results.RecordedChangeSets.Should().BeEmpty("the predicate never initialized");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SourceCompletesImmediately_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var predicateState = new Subject<object>();

            using var subscription = Observable.Empty<IChangeSet<Item>>()
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.HasCompleted.Should().BeTrue("no further changes can occur when there are no items to be filtered or unfiltered");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SourceErrors_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            var error = new Exception("This is a test.");
            source.OnError(error);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            source.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization of the stream");
            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SourceErrorsImmediately_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var predicateState = new Subject<object>();

            var error = new Exception("This is a test.");

            using var subscription = Observable.Throw<IChangeSet<Item>>(error)
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            results.Error.Should().Be(error, "errors should be propagated");
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");

            predicateState.HasObservers.Should().BeFalse("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Fact]
        public void SourceIsNull_ExceptionIsThrown()
            => FluentActions.Invoking(() => ObservableListEx.Filter(
                    source:         (null as IObservable<IChangeSet<Item>>)!,
                    predicateState: Observable.Empty<object>(),
                    predicate:      static (_, _) => true))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(ListFilterPolicy.CalculateDiff)]
        [InlineData(ListFilterPolicy.ClearAndReplace)]
        public void SubscriptionIsDisposed_UnsubscriptionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate:      static (predicateState, item) => item.IsIncluded,
                    filterPolicy:   filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


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
            using var source = new Subject<IChangeSet<Item>>();

            using var predicateState = new Subject<object>();

            using var subscription = source
                .Filter(
                    predicateState:             predicateState,
                    predicate:                  static (predicateState, item) => item.IsIncluded,
                    suppressEmptyChangeSets:    false)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);


            // Initialize the predicate
            predicateState.OnNext(new object());

            results.RecordedChangeSets.Count.Should().Be(1, "the predicate state was initialized");
            results.RecordedChangeSets[0].Should().BeEmpty("there are no items in the collection");
            ShouldBeValid(results, Enumerable.Empty<Item>());


            // Publish an empty changeset
            source.OnNext(ChangeSet<Item>.Empty);

            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "a source operation was performed");
            results.RecordedChangeSets.Skip(1).First().Should().BeEmpty("the source changeset was empty");
            ShouldBeValid(results, Enumerable.Empty<Item>());


            // Publish a changeset with only excluded items
            source.OnNext(new ChangeSet<Item>()
            {
                new(reason: ListChangeReason.AddRange,
                    items:  new[]
                    {
                        new Item() { Id = 1, IsIncluded = false },
                        new Item() { Id = 2, IsIncluded = false },
                        new Item() { Id = 3, IsIncluded = false }
                    },
                    index:  0)
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
            ListItemRecordingObserver<Item> results,
            IEnumerable<Item>               expectedFilteredItems)
        {
            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("no completion events should have occurred");
            results.RecordedItems.Should().BeEquivalentTo(expectedFilteredItems, "all filtered items should match the filter predicate");
        }

        private static (IList<Item> items, IReadOnlyList<IChangeSet<Item>> changeSets) GenerateStressItemsAndChangeSets(
            int         editCount,
            int         maxChangeCount,
            int         maxRangeSize,
            Randomizer  randomizer)
        {
            var changeReasons = new[]
            {
                ListChangeReason.Add,
                ListChangeReason.AddRange,
                ListChangeReason.Clear,
                ListChangeReason.Moved,
                ListChangeReason.Refresh,
                ListChangeReason.Remove,
                ListChangeReason.RemoveRange,
                ListChangeReason.Replace
            };

            // Weights are chosen to make the cache size likely to grow over time,
            // exerting more pressure on the system the longer the benchmark runs.
            // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
            var changeReasonWeightsWhenCountIs0 = new[]
            {
                0.5f, // Add
                0.5f, // AddRange
                0.0f, // Clear
                0.0f, // Moved
                0.0f, // Refresh
                0.0f, // Remove
                0.0f, // RemoveRange
                0.0f  // Replace
            };

            var changeReasonWeightsWhenCountIs1 = new[]
            {
                0.400f,  // Add
                0.400f,  // AddRange
                0.001f,  // Clear
                0.000f,  // Moved
                0.000f,  // Refresh
                0.199f,  // Remove
                0.000f,  // RemoveRange
                0.000f   // Replace
            };

            var changeReasonWeightsOtherwise = new[]
            {
                0.250f,  // Add
                0.250f,  // AddRange
                0.001f,  // Clear
                0.100f,  // Moved
                0.099f,  // Refresh
                0.100f,  // Remove
                0.100f,  // RemoveRange
                0.100f   // Replace
            };

            var nextItemId = 1;

            var changeSets = new List<IChangeSet<Item>>(capacity: editCount);

            var items = new ChangeAwareList<Item>();

            while (changeSets.Count < changeSets.Capacity)
            {
                var changeCount = randomizer.Int(1, maxChangeCount);
                for (var i = 0; i < changeCount; ++i)
                {
                    var changeReason = randomizer.WeightedRandom(changeReasons, items.Count switch
                    {
                        0 => changeReasonWeightsWhenCountIs0,
                        1 => changeReasonWeightsWhenCountIs1,
                        _ => changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ListChangeReason.Add:
                            items.Add(new Item()
                            {
                                Id = nextItemId++,
                                IsIncluded = randomizer.Bool()
                            });
                            break;

                        case ListChangeReason.AddRange:
                            items.AddRange(Enumerable.Repeat(0, randomizer.Int(1, maxRangeSize))
                                .Select(_ => new Item()
                                {
                                    Id = nextItemId++,
                                    IsIncluded = randomizer.Bool()
                                }));
                            break;

                        case ListChangeReason.Clear:
                            items.Clear();
                            break;

                        case ListChangeReason.Moved:
                            items.Move(
                                original: randomizer.Int(0, items.Count - 1),
                                destination: randomizer.Int(0, items.Count - 1));
                            break;

                        case ListChangeReason.Refresh:
                            items.RefreshAt(randomizer.Int(0, items.Count - 1));
                            break;

                        case ListChangeReason.Remove:
                            items.RemoveAt(randomizer.Int(0, items.Count - 1));
                            break;

                        case ListChangeReason.RemoveRange:
                            {
                                var rangeStartIndex = randomizer.Int(0, items.Count - 1);

                                items.RemoveRange(
                                    index: rangeStartIndex,
                                    count: Math.Min(items.Count - rangeStartIndex, randomizer.Int(1, maxRangeSize)));
                            }
                            break;

                        case ListChangeReason.Replace:
                            items[randomizer.Int(0, items.Count - 1)] = new Item()
                            {
                                Id = nextItemId++,
                                IsIncluded = randomizer.Bool()
                            };
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
