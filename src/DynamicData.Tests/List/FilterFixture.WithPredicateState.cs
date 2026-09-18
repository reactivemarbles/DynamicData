using Bogus;

namespace DynamicData.Tests.List;

public partial class FilterFixture
{
    public sealed class WithPredicateState
    {
        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task ChangesAreMadeAfterInitialPredicateState_ItemsAreFiltered(ListFilterPolicy filterPolicy)
        {
            using var source = new TestSourceList<Item>();
            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            // Set initial state
            predicateState.OnNext(new());

            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations have been performed");

            // Test Add, with an included item
            var item1 = new Item() { Id = 1, IsIncluded = true };
            source.Add(item1);

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed, with one included item added");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Add, with an excluded item
            var item2 = new Item() { Id = 2, IsIncluded = false };
            source.Add(item2);

            await Assert.That(results.RecordedChangeSets.Skip(1)).IsEmpty().Because("one source operation was performed, but no included items were affected");

            // Test AddRange, with both included and excluded items
            var item3 = new Item() { Id = 3, IsIncluded = false };
            var item4 = new Item() { Id = 4, IsIncluded = true };
            var item5 = new Item() { Id = 5, IsIncluded = true };
            var item6 = new Item() { Id = 6, IsIncluded = false };
            var item7 = new Item() { Id = 7, IsIncluded = false };
            var item8 = new Item() { Id = 8, IsIncluded = true };
            source.AddRange(new[] { item3, item4, item5, item6, item7, item8 });

            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed, with 3 included items added");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Refresh, with no item mutations.
            source.Refresh(Enumerable.Range(0, source.Count));

            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("one source operation was performed, with all included items affected");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Select(static change => change.Reason)).All(static reason => reason == ListChangeReason.Refresh).Because("all included items should have been refreshed");
            await Assert.That(results.RecordedChangeSets.Skip(2).First().Select(static change => change.Item.Current)).IsEquivalentTo(EnumerateFilteredItems()).Because("all included items should have been refreshed");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Refresh, with item mutations affecting filtering.
            item1.IsIncluded = !item1.IsIncluded;
            item3.IsIncluded = !item3.IsIncluded;
            item5.IsIncluded = !item5.IsIncluded;
            item6.IsIncluded = !item6.IsIncluded;
            source.Refresh(Enumerable.Range(0, source.Count));

            await Assert.That(results.RecordedChangeSets.Skip(3).Count()).IsEqualTo(1).Because("one source operation was performed, with items being included and excluded");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Remove, with an included item
            source.RemoveAt(3);

            await Assert.That(results.RecordedChangeSets.Skip(4).Count()).IsEqualTo(1).Because("one source operation was performed, with one included item affected");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Remove, with an excluded item
            source.RemoveAt(3);

            await Assert.That(results.RecordedChangeSets.Skip(5)).IsEmpty().Because("one source operation was performed, but no included items were affected");

            // Test Remove, with both included and excluded items
            source.RemoveRange(index: 2, count: 2);

            await Assert.That(results.RecordedChangeSets.Skip(5).Count()).IsEqualTo(1).Because("one source operation was performed, with one included item affected");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Replace, not affecting filtering
            var item9 = new Item() { Id = 9, IsIncluded = false };
            var item10 = new Item() { Id = 10, IsIncluded = true };
            source.Edit(updater =>
            {
                updater.Replace(item7, item9);
                updater.Replace(item8, item10);
            });

            await Assert.That(results.RecordedChangeSets.Skip(6).Count()).IsEqualTo(1).Because("one source operation was performed, with one included item affected");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Replace, affecting filtering
            var item11 = new Item() { Id = 11, IsIncluded = true };
            var item12 = new Item() { Id = 12, IsIncluded = false };
            source.Edit(updater =>
            {
                updater.Replace(item9, item11);
                updater.Replace(item10, item12);
            });

            await Assert.That(results.RecordedChangeSets.Skip(7).Count()).IsEqualTo(1).Because("one source operation was performed, with one included item affected");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Move of an included item, relative to another included item
            var item13 = new Item() { Id = 13, IsIncluded = true };
            source.Add(item13);
            source.Move(2, 4);

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    await Assert.That(results.RecordedChangeSets.Skip(8).Count()).IsEqualTo(2).Because("two source operations were performed");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    await Assert.That(results.RecordedChangeSets.Skip(8).Count()).IsEqualTo(1).Because("two source operations were performed, one of which was a move, which are not propagated, as ordering is not preserved");
                    break;
            }
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Move of an excluded item
            source.Move(4, 2);

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    await Assert.That(results.RecordedChangeSets.Skip(10).Count()).IsEqualTo(1).Because("one source operation was performed");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    await Assert.That(results.RecordedChangeSets.Skip(9)).IsEmpty().Because("one source operation was performed, a move, which are not propagated, as ordering is not preserved");
                    break;
            }

            // Test Clear, with included items
            source.Clear();

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    await Assert.That(results.RecordedChangeSets.Skip(11).Count()).IsEqualTo(1).Because("one source operation was performed, with all included items affected");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    await Assert.That(results.RecordedChangeSets.Skip(9).Count()).IsEqualTo(1).Because("one source operation was performed, with all included items affected");
                    break;
            }
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Test Clear, with only excluded items
            source.Add(new Item() { Id = 14, IsIncluded = false });
            source.Clear();

            switch (filterPolicy)
            {
                case ListFilterPolicy.CalculateDiff:
                    await Assert.That(results.RecordedChangeSets.Skip(12)).IsEmpty().Because("two source operations were performed, and neither affected included items");
                    break;

                case ListFilterPolicy.ClearAndReplace:
                    await Assert.That(results.RecordedChangeSets.Skip(10)).IsEmpty().Because("two source operations were performed, and neither affected included items");
                    break;
            }
            await ShouldBeValid(results, EnumerateFilteredItems());

            IEnumerable<Item> EnumerateFilteredItems()
                => source.Items.Where(static item => item.IsIncluded);
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task ChangesAreMadeAfterMultiplePredicateStateChanges_ItemsAreFilteredWithLatestPredicateState(ListFilterPolicy filterPolicy)
        {
            using var source = new SourceList<Item>();
            using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.Id == predicateState,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            // Publish multiple state changes
            predicateState.OnNext(2);
            predicateState.OnNext(3);

            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations have been performed");

            // Test filtering of items, by state
            source.AddRange(new[]
            {
                new Item() { Id = 1, IsIncluded = true },
                new Item() { Id = 2, IsIncluded = true },
                new Item() { Id = 3, IsIncluded = false },
                new Item() { Id = 4, IsIncluded = false }
            });

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed");
            await ShouldBeValid(results, source.Items.Where(item => item.Id == predicateState.Value));
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task ChangesAreMadeBeforeInitialPredicateState_ItemsAreFilteredOnPredicateState(ListFilterPolicy filterPolicy)
        {
            using var source = new TestSourceList<Item>();
            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations have been performed");

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

            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("the predicate state has not initialized");

            // Set initial state
            predicateState.OnNext(new());

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed");
            await ShouldBeValid(results, source.Items.Where(static item => item.IsIncluded));
        }

        [Test]
        public async Task FilterPolicyIsClearAndReplace_ReFilteringPreservesOrder()
        {
            using var source = new SourceList<Item>();
            using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.Id == predicateState,
                    filterPolicy: ListFilterPolicy.ClearAndReplace)
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

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed");

            // Capture the current set of filtered items, and publish a state change, to force a re-filter
            var priorFilteredItems = results.RecordedItems.ToArray();
            predicateState.OnNext(1);

            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await ShouldBeValid(results, source.Items.Where(item => item.Id == predicateState.Value));
            await Assert.That(results.RecordedItems).IsEquivalentTo(priorFilteredItems, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        [Test]
        public async Task PredicateIsNull_ExceptionIsThrown()
            => await Assert.That(() => Observable.Empty<IChangeSet<Item>>()
                    .Filter(
                        predicateState: Observable.Empty<object>(),
                        predicate: null!))
                .Throws<ArgumentNullException>();

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task PredicateStateChanges_ItemsAreReFiltered(ListFilterPolicy filterPolicy)
        {
            using var source = new SourceList<Item>();
            using var predicateState = new ReactiveUI.Primitives.Signals.StateSignal<int>(1);

            using var subscription = source
                .Connect()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.Id == predicateState,
                    filterPolicy: filterPolicy)
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

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("one source operation was performed");
            await ShouldBeValid(results, EnumerateFilteredItems());

            // Publish a state change, to change the filtering
            predicateState.OnNext(2);

            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("one source operation was performed");
            await ShouldBeValid(results, EnumerateFilteredItems());

            IEnumerable<Item> EnumerateFilteredItems()
                => source.Items.Where(item => item.Id == predicateState.Value);
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task PredicateStateCompletesAfterInitialValue_CompletionWaitsForSourceCompletion(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Return(new object()),
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.HasCompleted).IsFalse().Because("changes could still be generated by the source");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            source.OnCompleted();

            await Assert.That(results.HasCompleted).IsTrue().Because("all input streams have completed");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task PredicateStateCompletesImmediately_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var subscription = source
                .Filter(
                    predicateState: Observable.Empty<object>(),
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.HasCompleted).IsTrue().Because("completion of the predicate state stream before it emits any values means that items can never be accepted by the filter predicate");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(source.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task PredicateStateErrors_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();
            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            var error = new Exception("This is a test.");
            predicateState.OnError(error);

            await Assert.That(results.Error).IsEqualTo(error).Because("errors should be propagated");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(source.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization of the stream");
            await Assert.That(predicateState.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task PredicateStateErrorsImmediately_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            var error = new Exception("This is a test.");

            using var subscription = source
                .Filter(
                    predicateState: Observable.Throw<object>(error),
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsEqualTo(error).Because("errors should be propagated");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(source.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization of the stream");
        }

        [Test]
        public async Task PredicateStateIsNull_ExceptionIsThrown()
            => await Assert.That(() => Observable.Empty<IChangeSet<Item>>()
                    .Filter(
                        predicateState: (null as IObservable<object>)!,
                        predicate: static (_, _) => true))
                .Throws<ArgumentNullException>();

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceAndPredicateStateNotifyFromDifferentThreads_FilteringIsThreadSafe(ListFilterPolicy filterPolicy)
        {
            var randomizer = new Randomizer(0x1234567);

            (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                editCount: 5_000,
                maxChangeCount: 20,
                maxRangeSize: 10,
                randomizer: randomizer);

            var predicateStates = GenerateRandomPredicateStates(
                valueCount: 5_000,
                randomizer: randomizer);

            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<int>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: Item.FilterByIdInclusionMask,
                    filterPolicy: filterPolicy)
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
                    action: () =>
                    {
                        foreach (var changeSet in changeSets)
                            source.OnNext(changeSet);
                    },
                    cancellationToken: timeoutSource.Token),
                Task.Run(
                    action: () =>
                    {
                        foreach (var value in predicateStates)
                            predicateState.OnNext(value);
                    },
                    cancellationToken: timeoutSource.Token));

            var finalPredicateState = predicateStates[^1];
            await ShouldBeValid(results, items.Where(item => Item.FilterByIdInclusionMask(finalPredicateState, item)));
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceCompletesWhenEmpty_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            source.OnCompleted();

            await Assert.That(results.HasCompleted).IsTrue().Because("no further changes can occur when there are no items to be filtered or unfiltered");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(predicateState.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceCompletesWhenNotEmpty_CompletionWaitsForStateCompletion(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            source.OnNext(new ChangeSet<Item>() { new(reason: ListChangeReason.Add, current: new Item() { Id = 1, IsIncluded = true }, index: 0) });
            source.OnCompleted();

            await Assert.That(results.HasCompleted).IsFalse().Because("changes could still be generated by changes in predicate state");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("the predicate has not initialized");

            predicateState.OnCompleted();

            await Assert.That(results.HasCompleted).IsTrue().Because("all input streams have completed");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("the predicate never initialized");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceCompletesImmediately_CompletionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = Observable.Empty<IChangeSet<Item>>()
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.HasCompleted).IsTrue().Because("no further changes can occur when there are no items to be filtered or unfiltered");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(predicateState.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceErrors_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            var error = new Exception("This is a test.");
            source.OnError(error);

            await Assert.That(results.Error).IsEqualTo(error).Because("errors should be propagated");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(source.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization of the stream");
            await Assert.That(predicateState.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SourceErrorsImmediately_ErrorIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            var error = new Exception("This is a test.");

            using var subscription = Observable.Throw<IChangeSet<Item>>(error)
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            await Assert.That(results.Error).IsEqualTo(error).Because("errors should be propagated");
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");

            await Assert.That(predicateState.HasObservers).IsFalse().Because("all subscriptions should have been disposed, during finalization  of the stream");
        }

        [Test]
        public async Task SourceIsNull_ExceptionIsThrown()
            => await Assert.That(() => ObservableListEx.Filter(
                    source: (null as IObservable<IChangeSet<Item>>)!,
                    predicateState: Observable.Empty<object>(),
                    predicate: static (_, _) => true))
                .Throws<ArgumentNullException>();

        [Test]
        [Arguments(ListFilterPolicy.CalculateDiff)]
        [Arguments(ListFilterPolicy.ClearAndReplace)]
        public async Task SubscriptionIsDisposed_UnsubscriptionIsPropagated(ListFilterPolicy filterPolicy)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    filterPolicy: filterPolicy)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            subscription.Dispose();

            await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should be propagated to all input streams");
            await Assert.That(predicateState.HasObservers).IsFalse().Because("subscription disposal should be propagated to all input streams");

            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
        }

        [Test]
        [Arguments("source", "predicateState")]
        [Arguments("predicateState", "source")]
        public async Task SuppressEmptyChangeSetsIsFalse_EmptyChangesetsArePropagatedAndOnlyFinalCompletionIsPropagated(params string[] completionOrder)
        {
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item>>();

            using var predicateState = new ReactiveUI.Primitives.Signals.Signal<object>();

            using var subscription = source
                .Filter(
                    predicateState: predicateState,
                    predicate: static (predicateState, item) => item.IsIncluded,
                    suppressEmptyChangeSets: false)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .RecordListItems(out var results);

            // Initialize the predicate
            predicateState.OnNext(new object());

            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the predicate state was initialized");
            await Assert.That(results.RecordedChangeSets[0]).IsEmpty().Because("there are no items in the collection");
            await ShouldBeValid(results, Enumerable.Empty<Item>());

            // Publish an empty changeset
            source.OnNext(ChangeSet<Item>.Empty);

            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("a source operation was performed");
            await Assert.That(results.RecordedChangeSets.Skip(1).First()).IsEmpty().Because("the source changeset was empty");
            await ShouldBeValid(results, Enumerable.Empty<Item>());

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

            await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("a source operation was performed");
            await Assert.That(results.RecordedChangeSets.Skip(2).First()).IsEmpty().Because("all source items were excluded");
            await ShouldBeValid(results, Enumerable.Empty<Item>());

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
                    await Assert.That(results.HasCompleted).IsFalse().Because("not all input streams have completed");
            }

            await Assert.That(results.HasCompleted).IsTrue().Because("all input streams have completed");
        }

        private static async Task ShouldBeValid(
            ListItemRecordingObserver<Item> results,
            IEnumerable<Item> expectedFilteredItems)
        {
            await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
            await Assert.That(results.HasCompleted).IsFalse().Because("no completion events should have occurred");
            await Assert.That(results.RecordedItems).IsEquivalentTo(expectedFilteredItems).Because("all filtered items should match the filter predicate");
        }

        private static (IList<Item> items, IReadOnlyList<IChangeSet<Item>> changeSets) GenerateStressItemsAndChangeSets(
            int editCount,
            int maxChangeCount,
            int maxRangeSize,
            Randomizer randomizer)
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
            int valueCount,
            Randomizer randomizer)
        {
            var values = new List<int>(capacity: valueCount);

            while (values.Count < valueCount)
                values.Add(randomizer.Int());

            return values;
        }

        private class Item
        {
            public static bool FilterByIdInclusionMask(
                    int idInclusionMask,
                    Item item)
                => ((item.Id & idInclusionMask) == 0) && item.IsIncluded;

            public required int Id { get; init; }

            public bool IsIncluded { get; set; }

            public override string ToString()
                => $"{{ Id = {Id}, IsIncluded = {IsIncluded} }}";
        }
    }
}
