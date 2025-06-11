using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Reactive.Testing;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public static partial class ToObservableChangeSetFixture
{
    public static partial class Items
    {
        public class UnitTests
        {
            [Fact]
            public void ExpireAfterThrows_ErrorPropagates()
            {
                // Setup
                using var source = new Subject<Item>();

                var error = new Exception("Test Exception");


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(expireAfter: static item => (item.Error is not null)
                        ? throw item.Error
                        : item.Lifetime)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItems.Should().BeEmpty("no items have been emitted by the source");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1 };
                source.OnNext(item1);
                source.OnNext(new Item() { Id = 2, Error = error });
                source.OnNext(new Item() { Id = 3 });

                results.Error.Should().BeSameAs(error);
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item was emitted before an error occurred");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1 }, "1 item was emitted before an error occurred");
            }

            [Fact]
            public void SizeLimitIsExceeded_OldestItemsAreRemoved()
            {
                // Setup
                using var source = new Subject<Item>();


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(limitSizeTo: 5)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItems.Should().BeEmpty("no items have been emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: Not enough items to reach the limit
                var item1 = new Item() { Id = 1 };
                source.OnNext(item1);

                var item2 = new Item() { Id = 2 };
                source.OnNext(item2);

                var item3 = new Item() { Id = 3 };
                source.OnNext(item3);

                var item4 = new Item() { Id = 4 };
                source.OnNext(item4);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(4, "4 items were emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2, item3, item4 },
                    because: "4 items were emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: Limit is reached
                var item5 = new Item() { Id = 5 };
                source.OnNext(item5);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(5).Count().Should().Be(1, "1 item was emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2, item3, item4, item5 }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action: New item exceeds the limit
                var item6 = new Item() { Id = 6 };
                source.OnNext(item6);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(6).Count().Should().Be(1, "1 item was emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item2, item3, item4, item5, item6 }, 
                    because: "1 item was emitted, and 1 was evicted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");
            }

            [Theory]
            [InlineData(SourceType.Asynchronous)]
            [InlineData(SourceType.Immediate)]
            public void SourceCompletesWhenExpirationsArePending_CompletionWaitsForExpirations(SourceType sourceType)
            {
                // Setup
                var item = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(10) };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new Subject<Item>(),
                    SourceType.Immediate    => Observable.Return(item),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };
            
                var scheduler = new TestScheduler();


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is Subject<Item> subject)
                {
                    subject.OnNext(item);
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(2, "1 item was emitted, after initialization");
                else
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("1 item has yet to expire");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 item should have expired");
                else
                    results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item should have expired");
                results.RecordedItems.Should().BeEmpty("all items have expired");
                results.HasCompleted.Should().BeTrue("the source, and all outstanding expirations, have completed");
            }

            [Theory]
            [InlineData(SourceType.Asynchronous)]
            [InlineData(SourceType.Immediate)]
            public void SourceCompletesWhenNoExpirationsArePending_CompletionPropagates(SourceType sourceType)
            {
                // Setup
                var item = new Item() { Id = 1 };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new Subject<Item>(),
                    SourceType.Immediate    => Observable.Return(item),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };
            
                var scheduler = new TestScheduler();


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is Subject<Item> subject)
                {
                    subject.OnNext(item);
                    subject.OnCompleted();
                }

                results.Error.Should().BeNull();
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(2, "1 item was emitted, after initialization");
                else
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeTrue("the source has completed, and no items remain to be expired");
            }

            [Fact]
            public void SourceEmitsItems_ItemsAreAddedAndRemovedWhenExpired()
            {
                // Setup
                using var source = new Subject<Item>();

                var scheduler = new TestScheduler();


                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                results.RecordedItems.Should().BeEmpty("no items have been emitted");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(3) };
                source.OnNext(item1);
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item was emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1 }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item2 = new Item() { Id = 2 };
                source.OnNext(item2);
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(2).Count().Should().Be(1, "1 item was emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2 }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(1) };
                source.OnNext(item3);
                scheduler.AdvanceBy(1);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(3).Count().Should().Be(1, "1 item was emitted");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2, item3 }, 
                    because: "1 item was emitted",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(1).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(4).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2 }, 
                    because: "1 item expired, and 1 had its lifetime extended",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(2).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(5).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item1, item2 }, 
                    because: "no changes were made",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(3).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(5).Count().Should().Be(1, "1 expiration should have occurred");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item2 }, 
                    because: "1 item reached its expiration",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(4).Ticks);

                results.Error.Should().BeNull();
                results.RecordedChangeSets.Skip(6).Should().BeEmpty("no expirations should have occurred");
                results.RecordedItems.Should().BeEquivalentTo(new[] { item2 }, 
                    because: "no changes were made",
                    config: options => options.WithStrictOrdering());
                results.HasCompleted.Should().BeFalse("the source has not completed");
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
                    SourceType.Asynchronous => new Subject<Item>(),
                    SourceType.Immediate    => Observable.Throw<Item>(error),
                    _                       => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };


                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet()
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is Subject<Item> subject)
                    subject.OnError(error);

                results.Error.Should().BeSameAs(error, "errors should propagate");
                if (sourceType is SourceType.Asynchronous)
                    results.RecordedChangeSets.Count.Should().Be(1, "an initial changeset should always be emitted");
                else
                    results.RecordedChangeSets.Should().BeEmpty("an error occurred during initialization");
                results.RecordedItems.Should().BeEmpty("no items were emitted");
            }

            [Fact]
            public void SourceIsNull_ThrowsException()
                => FluentActions.Invoking(() => ObservableListEx.ToObservableChangeSet(
                        source: (null as IObservable<Item>)!))
                    .Should().Throw<ArgumentNullException>();
        }
    }
}
