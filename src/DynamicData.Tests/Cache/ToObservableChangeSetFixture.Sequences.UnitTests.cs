using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Reactive.Testing;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class ToObservableChangeSetFixture
{
    public static partial class Sequences
    {
        public class UnitTests
        {
            [Fact]
            public void ExpireAfterThrows_ErrorPropagates()
            {
                // Setup
                using var source = new Subject<IEnumerable<Item>>();

                var error = new Exception("Test Exception");


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    static item => (item.Error is not null)
                            ? throw item.Error
                            : item.Lifetime)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Should().BeEmpty("no items have been emitted by the source");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1 };
                source.OnNext(new[]
                {
                    item1,
                    new Item() { Id = 2, Error = error },
                    new Item() { Id = 3 }
                });

                results.Error.Should().BeSameAs(error);
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("an error occurred during processing of the sequence");
                results.RecordedItemsByKey.Values.Should().BeEmpty("an error occurred during processing of the sequence");


                results.ShouldNotSupportSorting($"Cache source operators do not support sorting");
            }

            [Fact]
            public void KeySelectorIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.ToObservableChangeSet<Item, int>(
                        source:         new Subject<IEnumerable<Item>>(),
                        keySelector:    null!))
                    .Should().Throw<ArgumentNullException>();

            [Fact]
            public void KeySelectorThrows_ErrorPropagates()
            {
                // Setup
                using var source = new Subject<IEnumerable<Item>>();

                var error = new Exception("Test Exception");


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(static item => (item.Error is not null)
                        ? throw item.Error
                        : item.Id)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Should().BeEmpty("no items have been emitted by the source");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1 };
                source.OnNext(new[]
                {
                    item1,
                    new Item() { Id = 2, Error = error },
                    new Item() { Id = 3 }
                });

                results.Error.Should().BeSameAs(error);
                results.RecordedChangeSets.Skip(1).Should().BeEmpty("an error occurred during processing of the sequence");
                results.RecordedItemsByKey.Values.Should().BeEmpty("an error occurred during processing of the sequence");


                results.ShouldNotSupportSorting($"Cache source operators do not support sorting");
            }

            [Fact]
            public void SizeLimitIsExceeded_OldestItemsAreRemoved()
            {
                // Setup
                using var source = new Subject<IEnumerable<Item>>();


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector: Item.SelectId,
                        limitSizeTo: 5)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Values.Should().BeEmpty("no source items have been emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: Not enough items to reach the limit
                var item1 = new Item() { Id = 1 };
                var item2 = new Item() { Id = 2 };
                var item3 = new Item() { Id = 3 };
                var item4 = new Item() { Id = 4 };
                source.OnNext(new[]
                {
                    item1,
                    item2,
                    item3,
                    item4
                });

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3, item4 }, "4 source items were emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: Limit is reached
                var item5 = new Item() { Id = 5 };
                source.OnNext(new[] { item5 });

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3, item4, item5 }, "1 source item was emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: New item exceeds the limit
                var item6 = new Item() { Id = 6 };
                source.OnNext(new[] { item6 });

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3, item4, item5, item6 }, "1 source item was emitted, and 1 was evicted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: Replacement leaves all other items in-place
                var item7 = new Item() { Id = 4 };
                source.OnNext(new[] { item7 });

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "1 source operation was performed");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2, item3, item7, item5, item6 }, "1 source item was emitted, and replaced an existing item.");
                results.HasCompleted.Should().BeFalse("the source has not completed");
            }

            [Theory]
            [InlineData(SourceType.Asynchronous)]
            [InlineData(SourceType.Immediate)]
            public void SourceCompletesWhenExpirationsArePending_CompletionWaitsForExpirations(SourceType sourceType)
            {
                // Setup
                var items = new[]
                {
                    new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(10) },
                    new Item() { Id = 2 },
                    new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(20) }
                };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new Subject<IEnumerable<Item>>(),
                    SourceType.Immediate    => Observable.Return<IEnumerable<Item>>(items),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };
            
                var scheduler = new TestScheduler();


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (source is Subject<IEnumerable<Item>> subject)
                {
                    subject.OnNext(items);
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(2, "1 sequence was emitted, after initialization");
                else
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "3 items were emitted");
                results.HasCompleted.Should().BeFalse("2 items have yet to expire");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(30).Ticks);

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Skip(2).Count().Should().Be(2, "2 items should have expired, at different times");
                else
                    results.RecordedChangeSets.Skip(1).Count().Should().Be(2, "2 items should have expired, at different times");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items.Where(static item => item.Lifetime is null), "all expirable items have expired");
                results.HasCompleted.Should().BeTrue("the source, and all outstanding expirations, have completed");


                results.ShouldNotSupportSorting();
            }

            [Theory]
            [InlineData(SourceType.Asynchronous)]
            [InlineData(SourceType.Immediate)]
            public void SourceCompletesWhenNoExpirationsArePending_CompletionPropagates(SourceType sourceType)
            {
                // Setup
                var items = new[]
                {
                    new Item() { Id = 1 },
                    new Item() { Id = 2 },
                    new Item() { Id = 3 }
                };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new Subject<IEnumerable<Item>>(),
                    SourceType.Immediate    => Observable.Return<IEnumerable<Item>>(items),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };
            
                var scheduler = new TestScheduler();


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (source is Subject<IEnumerable<Item>> subject)
                {
                    subject.OnNext(items);
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(2, "1 sequence was emitted, after initialization");
                else
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items, "3 items were emitted");
                results.HasCompleted.Should().BeTrue("the source has completed, and no items remain to be expired");


                results.ShouldNotSupportSorting();
            }

            [Fact]
            public void SourceEmitsRepeatedItems_RepeatedItemsAreUpdatedAndExpirationIsRescheduled()
            {
                // Setup
                using var source = new Subject<IEnumerable<Item>>();

                var scheduler = new TestScheduler();


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Values.Should().BeEmpty("no source items have been emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(1) };
                var item2 = new Item() { Id = 2 };
                var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(1) };
                source.OnNext(new[] { item1, item2, item3 });
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 sequence was emitted");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item4 = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(1) };
                var item5 = new Item() { Id = 2 };
                var item6 = new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(3) };
                source.OnNext(new[] { item4, item5, item6 });
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 sequence was emitted");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item4, item5, item6 }, "3 items were emitted, and replaced existing items");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(1).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item5, item6 }, "1 source item reached its expiration");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(2).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(4).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item5, item6 }, "no changes were made");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(3).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item5 }, "1 source item reached its expiration");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(4).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(5).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item5 }, "no changes were made");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                results.ShouldNotSupportSorting();
            }

            [Fact]
            public void SourceEmitsUniqueItems_ItemsAreAddedAndRemovedWhenExpired()
            {
                // Setup
                using var source = new Subject<IEnumerable<Item>>();

                var scheduler = new TestScheduler();


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItemsByKey.Values.Should().BeEmpty("no source items have been emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(3) };
                var item2 = new Item() { Id = 2 };
                var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(1) };
                source.OnNext(new[] { item1, item2, item3 });
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 sequence was emitted");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2, item3 }, "3 items were emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(1).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2 }, "1 item expired, and 1 had its lifetime extended");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(2).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(3).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item1, item2 }, "no changes were made");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(3).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2 }, "1 item reached its expiration");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(4).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(4).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(new[] { item2 }, "no changes were made");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                results.ShouldNotSupportSorting();
            }

            [Theory]
            [InlineData(SourceType.Asynchronous)]
            [InlineData(SourceType.Immediate)]
            public void SourceFails_ErrorPropagates(SourceType sourceType)
            {
                // Setup
                var error = new Exception("Test Exception");

                var source = sourceType switch
                { 
                    SourceType.Asynchronous => new Subject<IEnumerable<Item>>(),
                    SourceType.Immediate    => Observable.Throw<IEnumerable<Item>>(error),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(Item.SelectId)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);

                if (source is Subject<IEnumerable<Item>> subject)
                    subject.OnError(error);

                results.Error.Should().BeSameAs(error, "errors should propagate");
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                else
                    results.RecordedChangeSets.Should().BeEmpty("an error occurred during initialization");
                results.RecordedItemsByKey.Values.Should().BeEmpty("no source items were emitted");
            }

            [Fact]
            public void SourceIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableCacheEx.ToObservableChangeSet(
                        source:         (null as IObservable<IEnumerable<Item>>)!,
                        keySelector:    Item.SelectId))
                    .Should().Throw<ArgumentNullException>();
        }
    }
}
