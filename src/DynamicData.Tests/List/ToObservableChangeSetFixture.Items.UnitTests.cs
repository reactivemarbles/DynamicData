namespace DynamicData.Tests.List;

public static partial class ToObservableChangeSetFixture
{
    public static partial class Items
    {
        public class UnitTests
        {
            [Test]
            public async Task ExpireAfterThrows_ErrorPropagates()
            {
                // Setup
                using var source = new ReactiveUI.Primitives.Signals.Signal<Item>();

                var error = new Exception("Test Exception");

                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(expireAfter: static item => (item.Error is not null)
                        ? throw item.Error
                        : item.Lifetime)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been emitted by the source");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                var item1 = new Item() { Id = 1 };
                source.OnNext(item1);
                source.OnNext(new Item() { Id = 2, Error = error });
                source.OnNext(new Item() { Id = 3 });

                await Assert.That(results.Error).IsSameReferenceAs(error);
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 item was emitted before an error occurred");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1 }).Because("1 item was emitted before an error occurred");
            }

            [Test]
            public async Task SizeLimitIsExceeded_OldestItemsAreRemoved()
            {
                // Setup
                using var source = new ReactiveUI.Primitives.Signals.Signal<Item>();

                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(limitSizeTo: 5)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action: Not enough items to reach the limit
                var item1 = new Item() { Id = 1 };
                source.OnNext(item1);

                var item2 = new Item() { Id = 2 };
                source.OnNext(item2);

                var item3 = new Item() { Id = 3 };
                source.OnNext(item3);

                var item4 = new Item() { Id = 4 };
                source.OnNext(item4);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(4).Because("4 items were emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2, item3, item4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("4 items were emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action: Limit is reached
                var item5 = new Item() { Id = 5 };
                source.OnNext(item5);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(5).Count()).IsEqualTo(1).Because("1 item was emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2, item3, item4, item5 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action: New item exceeds the limit
                var item6 = new Item() { Id = 6 };
                source.OnNext(item6);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(6).Count()).IsEqualTo(1).Because("1 item was emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item2, item3, item4, item5, item6 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted, and 1 was evicted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
            }

            [Test]
            [Arguments(SourceType.Asynchronous)]
            [Arguments(SourceType.Immediate)]
            public async Task SourceCompletesWhenExpirationsArePending_CompletionWaitsForExpirations(SourceType sourceType)
            {
                // Setup
                var item = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(10) };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new ReactiveUI.Primitives.Signals.Signal<Item>(),
                    SourceType.Immediate => Observable.Return(item),
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };

                var scheduler = new TestScheduler();

                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter: Item.SelectLifetime,
                        scheduler: scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is ReactiveUI.Primitives.Signals.Signal<Item> subject)
                {
                    subject.OnNext(item);
                    subject.OnCompleted();
                }

                await Assert.That(results.Error).IsNull();
                if (sourceType is SourceType.Asynchronous)
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(2).Because("1 item was emitted, after initialization");
                else
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("1 item has yet to expire");

                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(10).Ticks);

                await Assert.That(results.Error).IsNull();
                if (sourceType is SourceType.Asynchronous)
                    await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 item should have expired");
                else
                    await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 item should have expired");
                await Assert.That(results.RecordedItems).IsEmpty().Because("all items have expired");
                await Assert.That(results.HasCompleted).IsTrue().Because("the source, and all outstanding expirations, have completed");
            }

            [Test]
            [Arguments(SourceType.Asynchronous)]
            [Arguments(SourceType.Immediate)]
            public async Task SourceCompletesWhenNoExpirationsArePending_CompletionPropagates(SourceType sourceType)
            {
                // Setup
                var item = new Item() { Id = 1 };

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new ReactiveUI.Primitives.Signals.Signal<Item>(),
                    SourceType.Immediate => Observable.Return(item),
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };

                var scheduler = new TestScheduler();

                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter: Item.SelectLifetime,
                        scheduler: scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is ReactiveUI.Primitives.Signals.Signal<Item> subject)
                {
                    subject.OnNext(item);
                    subject.OnCompleted();
                }

                await Assert.That(results.Error).IsNull();
                if (sourceType is SourceType.Asynchronous)
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(2).Because("1 item was emitted, after initialization");
                else
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed, and no items remain to be expired");
            }

            [Test]
            public async Task SourceEmitsItems_ItemsAreAddedAndRemovedWhenExpired()
            {
                // Setup
                using var source = new ReactiveUI.Primitives.Signals.Signal<Item>();

                var scheduler = new TestScheduler();

                // UUT Initialization
                using var subscription = source
                    .ToObservableChangeSet(
                        expireAfter: Item.SelectLifetime,
                        scheduler: scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                await Assert.That(results.RecordedItems).IsEmpty().Because("no items have been emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                var item1 = new Item() { Id = 1, Lifetime = TimeSpan.FromSeconds(3) };
                source.OnNext(item1);
                scheduler.AdvanceBy(1);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 item was emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                var item2 = new Item() { Id = 2 };
                source.OnNext(item2);
                scheduler.AdvanceBy(1);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(2).Count()).IsEqualTo(1).Because("1 item was emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                var item3 = new Item() { Id = 3, Lifetime = TimeSpan.FromSeconds(1) };
                source.OnNext(item3);
                scheduler.AdvanceBy(1);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(3).Count()).IsEqualTo(1).Because("1 item was emitted");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2, item3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item was emitted");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(1).Ticks);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(4).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item expired, and 1 had its lifetime extended");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(2).Ticks);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(5)).IsEmpty().Because("no expirations should have occurred");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item1, item2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("no changes were made");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(3).Ticks);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(5).Count()).IsEqualTo(1).Because("1 expiration should have occurred");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("1 item reached its expiration");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // UUT Action
                scheduler.AdvanceTo(TimeSpan.FromSeconds(4).Ticks);

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedChangeSets.Skip(6)).IsEmpty().Because("no expirations should have occurred");
                await Assert.That(results.RecordedItems).IsEquivalentTo(new[] { item2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("no changes were made");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
            }

            [Test]
            [Arguments(SourceType.Asynchronous)]
            [Arguments(SourceType.Immediate)]
            public async Task SourceFails_ErrorPropagates(SourceType sourceType)
            {
                // Setup
                var error = new Exception("Test Exception");

                var source = sourceType switch
                {
                    SourceType.Asynchronous => new ReactiveUI.Primitives.Signals.Signal<Item>(),
                    SourceType.Immediate => Observable.Throw<Item>(error),
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
                };

                // UUT Initialization & Action
                using var subscription = source
                    .ToObservableChangeSet()
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results);

                if (source is ReactiveUI.Primitives.Signals.Signal<Item> subject)
                    subject.OnError(error);

                await Assert.That(results.Error).IsSameReferenceAs(error).Because("errors should propagate");
                if (sourceType is SourceType.Asynchronous)
                    await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("an initial changeset should always be emitted");
                else
                    await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred during initialization");
                await Assert.That(results.RecordedItems).IsEmpty().Because("no items were emitted");
            }

            [Test]
            public async Task SourceIsNull_ThrowsException()
                => await Assert.That(() => ObservableListEx.ToObservableChangeSet(
                        source: (null as IObservable<Item>)!))
                    .Throws<ArgumentNullException>();
        }
    }
}
