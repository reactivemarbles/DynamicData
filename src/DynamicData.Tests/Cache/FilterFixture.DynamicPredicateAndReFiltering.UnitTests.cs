using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicateAndReFiltering
    {
        public sealed class UnitTests
            : Base
        {
            [Theory]
            [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void ChangesAreMadeBeforeInitialPredicateChangedValue_ItemsAreExcluded(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);


                // UUT Initialization
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:           Observable.Never<Func<Item, bool>>(),
                        reapplyFilter:              Observable.Never<Unit>(),
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

            [Theory]
            [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void PredicateChangedChanges_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source            = new TestSourceCache<Item, int>(Item.SelectId);
                using var predicateChanged  = new BehaviorSubject<Func<Item, bool>>(Item.FilterByIsIncluded);

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
                        predicateChanged:           predicateChanged,
                        reapplyFilter:              Observable.Never<Unit>(),
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                predicateChanged.OnNext(Item.FilterByEvenId);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 predicate change occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByEvenId), "newly-matching items should have been added, and newly-excluded items should have been removed");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.Source)]
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.ReapplyFilter)]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.Source)]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.ReapplyFilter)]
            public void PredicateChangedCompletesAfterInitialValue_CompletionWaitsForSourceAndReapplyFilterCompletion(
                CompletionStrategy  completionStrategy,
                DynamicParameter    lastCompletion)
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

                var predicateChanged = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<Func<Item, bool>>()
                    : Observable.Return(Item.FilterByIsIncluded);

                var reapplyFilter = new Subject<Unit>();


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:   predicateChanged,
                        reapplyFilter:      reapplyFilter)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is Subject<Func<Item, bool>> subject)
                {
                    subject.OnNext(Item.FilterByIsIncluded);
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial predicate, was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("changes could still be generated by the source");


                // UUT Action (second completion)
                if (lastCompletion is DynamicParameter.ReapplyFilter)
                    source.Complete();
                else
                    reapplyFilter.OnCompleted();

                results.Error.Should().BeNull();
                if (completionStrategy is CompletionStrategy.Asynchronous)
                    results.RecordedChangeSets.Skip(2).Should().BeEmpty("no source operations were performed");
                else
                    results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeFalse("changes could still be generated by the filtering sources");


                // UUT Action (last completion)
                if (lastCompletion is DynamicParameter.ReapplyFilter)
                    reapplyFilter.OnCompleted();
                else
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
            public void PredicateChangedCompletesBeforeInitialValue_CompletionPropagatesIfEmptyChangesetsAreSuppressed(
                CompletionStrategy      completionStrategy,
                EmptyChangesetPolicy    emptyChangesetPolicy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var predicateChanged = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<Func<Item, bool>>()
                    : Observable.Empty<Func<Item, bool>>();


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:           predicateChanged,
                        reapplyFilter:              Observable.Never<Unit>(),
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is Subject<Func<Item, bool>> subject)
                    subject.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                if (emptyChangesetPolicy is EmptyChangesetPolicy.IncludeEmptyChangesets)
                    results.HasCompleted.Should().BeFalse("the source has completed, but further empty changesets can occur");
                else
                    results.HasCompleted.Should().BeTrue("the source has completed, and no further changesets can occur");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous)]
            [InlineData(CompletionStrategy.Immediate)]
            public void PredicateChangedFails_ErrorPropagates(CompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var error = new Exception("Test");

                var predicateChanged = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<Func<Item, bool>>()
                    : Observable.Throw<Func<Item, bool>>(error);


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:   predicateChanged,
                        reapplyFilter:      Observable.Never<Unit>())
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (predicateChanged is Subject<Func<Item, bool>> subject)
                    subject.OnError(error);

                results.Error.Should().Be(error, "errors should propagate downstream");
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            }

            [Fact]
            public void PredicateChangedIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.Filter(
                        source:             Observable.Return(ChangeSet<Item, int>.Empty),
                        reapplyFilter:      Observable.Never<Unit>(),
                        predicateChanged:   null!))
                    .Should()
                    .Throw<ArgumentNullException>();

            [Theory]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.PredicateChanged)]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.Source)]
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.PredicateChanged)]
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.Source)]
            public void ReapplyFilterCompletes_CompletionWaitsForSourceAndPredicateChangedCompletion(
                CompletionStrategy  completionStrategy,
                DynamicParameter    lastCompletion)
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

                var predicateChanged = new BehaviorSubject<Func<Item, bool>>(Item.FilterByIsIncluded);

                var reapplyFilter = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<Unit>()
                    : Observable.Empty<Unit>();


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:   predicateChanged,
                        reapplyFilter:      reapplyFilter)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (reapplyFilter is Subject<Unit> subject)
                    subject.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("changes could still be generated by the source");


                // UUT Action (second completion)
                if (lastCompletion is DynamicParameter.PredicateChanged)
                    source.Complete();
                else
                    predicateChanged.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeFalse("changes could still be generated by other source streams");


                // UUT Action (last completion)
                if (lastCompletion is DynamicParameter.PredicateChanged)
                    predicateChanged.OnCompleted();
                else
                    source.Complete();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeTrue("all input streams have completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }

            [Theory]
            [InlineData(CompletionStrategy.Asynchronous)]
            [InlineData(CompletionStrategy.Immediate)]
            public void ReapplyFilterFails_ErrorPropagates(CompletionStrategy completionStrategy)
            {
                // Setup
                using var source = new TestSourceCache<Item, int>(Item.SelectId);

                var error = new Exception("Test");

                var reapplyFilter = (completionStrategy is CompletionStrategy.Asynchronous)
                    ? new Subject<Unit>()
                    : Observable.Throw<Unit>(error);


                // UUT Initialization & Action
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:   Observable.Return(Item.FilterByIsIncluded),
                        reapplyFilter:      reapplyFilter)
                    .ValidateSynchronization()
                    .ValidateChangeSets(static item => item.Id)
                    .RecordCacheItems(out var results);

                if (reapplyFilter is Subject<Unit> subject)
                    subject.OnError(error);

                results.Error.Should().Be(error, "errors should propagate downstream");
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            }

            [Fact]
            public void ReapplyFilterIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.Filter(
                        source:             Observable.Return(ChangeSet<Item, int>.Empty),
                        predicateChanged:   Observable.Return(Item.FilterByIsIncluded),
                        reapplyFilter:      null!))
                    .Should()
                    .Throw<ArgumentNullException>();

            [Theory]
            [InlineData(EmptyChangesetPolicy.IncludeEmptyChangesets)]
            [InlineData(EmptyChangesetPolicy.SuppressEmptyChangesets)]
            public void ReapplyFilterOccurs_ItemsAreReFiltered(EmptyChangesetPolicy emptyChangesetPolicy)
            {
                // Setup
                using var source        = new TestSourceCache<Item, int>(Item.SelectId);
                using var reapplyFilter = new Subject<Unit>();

                source.AddOrUpdate(new[]
                {
                    new Item() { Id = 1, IsIncluded = true },
                    new Item() { Id = 2, IsIncluded = true },
                    new Item() { Id = 3, IsIncluded = true },
                    new Item() { Id = 4, IsIncluded = false },
                    new Item() { Id = 5, IsIncluded = false },
                    new Item() { Id = 6, IsIncluded = false }
                });

                var predicate = Item.FilterByIsIncluded;


                // UUT Initialization
                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:           Observable.Return<Func<Item, bool>>(item => predicate.Invoke(item)),
                        reapplyFilter:              reapplyFilter,
                        suppressEmptyChangeSets:    emptyChangesetPolicy is EmptyChangesetPolicy.SuppressEmptyChangesets)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(predicate), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                source.Items[1].IsIncluded = false;
                source.Items[5].IsIncluded = true;
                predicate = Item.FilterByEvenId;
                reapplyFilter.OnNext(Unit.Default);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 re-filter request occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(predicate), "newly-matching items should have been added, and newly-excluded items should have been removed");
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
                        predicateChanged:           Observable.Concat(
                            Observable.Return(Item.FilterByIsIncluded),
                            Observable.Never<Func<Item, bool>>()),
                        reapplyFilter:              Observable.Never<Unit>(),
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
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.PredicateChanged)]
            [InlineData(CompletionStrategy.Asynchronous,    DynamicParameter.ReapplyFilter)]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.PredicateChanged)]
            [InlineData(CompletionStrategy.Immediate,       DynamicParameter.ReapplyFilter)]
            public void SourceCompletesWhenNotEmpty_CompletionWaitsForPredicateChangedAndReapplyFilterCompletion(
                CompletionStrategy  completionStrategy,
                DynamicParameter    lastCompletion)
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

                using var predicateChanged  = new BehaviorSubject<Func<Item, bool>>(Item.FilterByIsIncluded);
                using var reapplyFilter     = new Subject<Unit>();


                // UUT Initialization & Action
                if (completionStrategy is CompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect()
                    .Filter(
                        predicateChanged:   predicateChanged,
                        reapplyFilter:      reapplyFilter)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (completionStrategy is CompletionStrategy.Asynchronous)
                    source.Complete();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset was published");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "all matching items should have propagated");
                results.HasCompleted.Should().BeFalse("the collection could still change due to new predicates");


                // UUT Action (second completion)
                if (lastCompletion is DynamicParameter.PredicateChanged)
                    reapplyFilter.OnCompleted();
                else
                    predicateChanged.OnCompleted();

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("no source operations were performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items.Where(Item.FilterByIsIncluded), "no changes should have been made");
                results.HasCompleted.Should().BeFalse("the collection could still change due to outstanding source streams");


                // UUT Action (last completion)
                if (lastCompletion is DynamicParameter.PredicateChanged)
                    predicateChanged.OnCompleted();
                else
                    reapplyFilter.OnCompleted();

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
                using var predicateChanged  = new BehaviorSubject<Func<Item, bool>>(Item.FilterByIsIncluded);
                using var reapplyFilter     = new Subject<Unit>();


                // UUT Initialization
                using var subscription = source
                    .Filter(
                        predicateChanged:   predicateChanged,
                        reapplyFilter:      reapplyFilter)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                subscription.Dispose();

                source          .HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
                predicateChanged.HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
                reapplyFilter   .HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
            }

            protected override IObservable<IChangeSet<Item, int>> BuildUut(
                    IObservable<IChangeSet<Item, int>>  source,
                    Func<Item, bool>                    predicate,
                    bool                                suppressEmptyChangeSets)
                => source.Filter(
                    predicateChanged:           Observable.Return(predicate),
                    reapplyFilter:              Observable.Never<Unit>(),
                    suppressEmptyChangeSets:    suppressEmptyChangeSets);
        }
    }
}
