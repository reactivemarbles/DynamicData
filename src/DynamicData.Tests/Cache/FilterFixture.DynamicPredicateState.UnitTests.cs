using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicateState
    {
        public sealed class UnitTests
            : Base
        {
            [Theory]
            [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void ChangesAreMadeBeforeInitialPredicateStateValue_ItemsAreExcluded(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);


                // UUT Initialization
                using var subscription = source
                    .Connect()
                    .Filter(
                        predicate:                  static (_, item) => item.IsIncluded,
                        predicateState:             Observable.Never<object>(),
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                // Add changes
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = false },
                    new Item() { Id = 4, IsIncluded = false }
                });

                // Refresh changes, with no item mutations.
                source.Refresh();

                // Refresh changes, with item mutations affecting filtering.
                foreach (var item in source.Items)
                    item.IsIncluded = !item.IsIncluded;
                source.Refresh();

                // Remove changes
                source.RemoveKeys(new[] { 2, 3 });

                // Update changes, not affecting filtering
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = false },
                    new Item() { Id = 4, IsIncluded = true }
                });

                // Update changes, affecting filtering
                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false }
                });

                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    results.RecordedChangeSets.Count.Should().Be(6, "6 source operations were performed");
                else
                    results.RecordedChangeSets.Should().BeEmpty("empty changesets should be suppressed");
                results.RecordedItemsByKey.Should().BeEmpty("the predicate has not initialized");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Fact]
            public void PredicateIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.Filter(
                        source:         Observable.Return(ChangeSet<Item, int>.Empty),
                        predicate:      null!,
                        predicateState: Observable.Empty<object>()))
                    .Should()
                    .Throw<ArgumentNullException>();

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous)]
            [InlineData(CompletionStrategy.Immediate)]
            public void PredicateStateCompletesAfterInitialValue_CompletionWaitsForSourceCompletion(CompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                var predicateState = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<object>()
                    : Observable.Return(new object());


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate:      static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateState is Subject<object> subject)
                {
                    subject.OnNext(new());
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial predicate, was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("changes could still be generated by the source");


                // UUT Action
                source.Complete();

                results.Error.Should().BeNull();
                if (completionStrategy is CompletionStrategy.Asynchronous)
                    results.RecordedChangeSets.Skip(2).Should().BeEmpty("no source operations were performed");
                else
                    results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeTrue("all source streams have completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Theory]
            [InlineData(CompletionStrategy.Immediate,       EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(CompletionStrategy.Immediate,       EmptyChangesetPolicy.SuppressEmptyChangesets)]
            [InlineData(CompletionStrategy.Asynchronous,    EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(CompletionStrategy.Asynchronous,    EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void PredicateStateCompletesBeforeInitialValue_CompletionPropagatesIfEmptyChangesetsAreSuppressed(
                CompletionStrategy      completionStrategy,
                EmptyChangesetPolicy    emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var predicateState = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<object>()
                    : Observable.Empty<object>();


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate:                  static (_, item) => item.IsIncluded,
                        predicateState:             predicateState,
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateState is Subject<object> subject)
                    subject.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    results.HasCompleted.Should().BeFalse("additional empty changesets can occur");
                else
                    results.HasCompleted.Should().BeTrue("only empty changesets can occur");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous)]
            [InlineData(CompletionStrategy.Immediate)]
            public void PredicateStateFails_ErrorPropagates(CompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var error = new Exception("Test");

                var predicateState = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<object>()
                    : Observable.Throw<object>(error);


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicate:      static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (predicateState is Subject<object> subject)
                    subject.OnError(error);

                results.Error.Should().Be(error, "errors should propagate downstream");
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            }

            [Fact]
            public void PredicateStateIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.Filter(
                        source:             Observable.Return(ChangeSet<Item, int>.Empty),
                        predicate:          static (object _, Item item) => item.IsIncluded,
                        predicateState:     null!))
                    .Should()
                    .Throw<ArgumentNullException>();

            [Theory]
            [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void PredicateStateChanges_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source            = new TestSourceCache<Item, int>(Item.SelectId);
                using var predicateState    = new BehaviorSubject<int>(0x5);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });


                // UUT Initialization
                using var subscription = source.Connect()
                    .Filter(
                        predicate:                  Item.FilterByIdInclusionMask,
                        predicateState:             predicateState,
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(item => Item.FilterByIdInclusionMask(predicateState.Value, item)), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                predicateState.OnNext(0xA);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 predicate change occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(item => Item.FilterByIdInclusionMask(predicateState.Value, item)), "newly-matching items should have been added, and newly-excluded items should have been removed");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous,    EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(CompletionStrategy.Asynchronous,    EmptyChangesetPolicy.SuppressEmptyChangesets)]
            [InlineData(CompletionStrategy.Immediate,       EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(CompletionStrategy.Immediate,       EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void SourceCompletesWhenEmpty_CompletionPropagatesWhenEmptyChangesetsAreSuppressed(
                CompletionStrategy      completionStrategy,
                EmptyChangesetPolicy    emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);


                // UUT Initialization & Action
                if (completionStrategy is CompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(
                        predicate:                  static (_, item) => item.IsIncluded,
                        predicateState:             Observable.Concat(
                            Observable.Return(new object()),
                            Observable.Never<object>()),
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (completionStrategy is CompletionStrategy.Asynchronous)
                    source.Complete();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    results.HasCompleted.Should().BeFalse("the source has completed, but further empty changesets can occur");
                else
                    results.HasCompleted.Should().BeTrue("the source has completed, and no further changesets can occur");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous)]
            [InlineData(CompletionStrategy.Immediate)]
            public void SourceCompletesWhenNotEmpty_CompletionWaitsForPredicateChangedCompletion(CompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                using var predicateState = new BehaviorSubject<object>(new object());


                // UUT Initialization & Action
                if (completionStrategy is CompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(
                        predicate:      static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (completionStrategy is CompletionStrategy.Asynchronous)
                    source.Complete();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("the collection could still change due to new predicates");


                // UUT Action
                predicateState.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "no changes should have been made");
                results.HasCompleted.Should().BeTrue("all source streams have completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Fact]
            public void SubscriptionIsDisposed_SubscriptionDisposalPropagates()
            {
                // Setup
                using var source            = new Subject<IChangeSet<Item, int>>();
                using var predicateState    = new BehaviorSubject<object>(new());


                // UUT Initialization
                using var subscription = source
                    .Filter(
                        predicate:      static (_, item) => item.IsIncluded,
                        predicateState: predicateState)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                subscription.Dispose();

                source          .HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
                predicateState  .HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
            }

            protected override IObservable<IChangeSet<Item, int>> BuildUut(
                    IObservable<IChangeSet<Item, int>>  source,
                    Func<Item, bool>                    predicate,
                    bool                                suppressEmptyChangeSets)
                => source.Filter(
                    predicate:                  (_, item) => predicate.Invoke(item),
                    predicateState:             Observable.Return(new object()),
                    suppressEmptyChangeSets:    suppressEmptyChangeSets);
        }
    }
}
