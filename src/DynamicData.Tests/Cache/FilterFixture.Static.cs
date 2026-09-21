using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Randomizer = Bogus.Randomizer;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public sealed class Static
        : Base
    {
        private const int RemoveKeySeed = 0x1165;

        public Static(ITestOutputHelper output)
            => output.WriteLine($"Bogus seed: {RemoveKeySeed}");

        [Fact]
        public void FilterIsNull_ThrowsException()
            => FluentActions.Invoking(static () => ObservableCacheEx.Filter(
                    source:     Observable.Empty<IChangeSet<Item, int>>(),
                    filter:     null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(StreamCompletionStrategy.Asynchronous)]
        [InlineData(StreamCompletionStrategy.Immediate)]
        public void SourceCompletes_CompletionPropagates(StreamCompletionStrategy completionStrategy)
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);


            // UUT Initialization & Action
            if (completionStrategy is StreamCompletionStrategy.Immediate)
                source.Complete();

            using var subscription = source.Connect(suppressEmptyChangeSets: false)
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                source.Complete();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.HasCompleted.Should().BeTrue("the source has completed");


            // Final verification
            results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Fact]
        public void SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new Subject<IChangeSet<Item, int>>();


            // UUT Intialization
            using var subscription = source
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Should().BeEmpty("no source operations were performed");
            results.RecordedItemsByKey.Values.Should().BeEmpty("the source has not initialized");
            results.HasCompleted.Should().BeFalse("the source has not completed");


            // UUT Action
            subscription.Dispose();

            source.HasObservers.Should().BeFalse("subscription disposal should propagate to all sources");
        }

        protected override IObservable<IChangeSet<Item, int>> BuildUut(
                IObservable<IChangeSet<Item, int>>  source,
                Func<Item, bool>                    predicate,
                bool                                suppressEmptyChangeSets)
            => source.Filter(
                filter:                     predicate,
                suppressEmptyChangeSets:    suppressEmptyChangeSets);

        /// <summary>
        /// List filtering after RemoveKey must remove every item that stops matching, not retain a stale superset.
        /// </summary>
        [Fact]
        public void AutoRefreshRemoveKeyFilterUpdate_CollectionUpdated()
        {
            // Arrange: all generated items start below a derived threshold.
            var randomizer = new Randomizer(RemoveKeySeed);
            using var source = new TestSourceCache<Person, string>(static person => person.Key);
            var people = Fakers.Person.Clone()
                .UseSeed(randomizer.Int())
                .Generate(randomizer.Int(3, 8))
                .ToArray();
            var exclusiveAge = people.Max(static person => person.Age) + 1;
            ReadOnlyObservableCollection<Person> collection;
            using var subscription = source.Connect()
                .AutoRefresh(x => x.Age)
                .RemoveKey()
                .Filter(person => person.Age < exclusiveAge)
                .ValidateSynchronization()
                .ValidateChangeSets()
                .Bind(out collection)
                .Subscribe();

            // Act: add the initial matching collection.
            source.AddOrUpdate(people);

            // Assert: the complete initial membership is present.
            Assert.Equivalent(people, collection, strict: true);

            // Act: guarantee one removal while other matching items remain.
            people[0].Age = exclusiveAge;

            // Assert: a stale entry must not be accepted as an extra member.
            Assert.Equivalent(people.Where(person => person.Age < exclusiveAge), collection, strict: true);

            // Act: exclude every remaining item.
            foreach (var person in people)
            {
                person.Age = exclusiveAge;
            }

            // Assert: strict comparison also rejects stale items when the expectation is empty.
            Assert.Equivalent(people.Where(person => person.Age < exclusiveAge), collection, strict: true);
        }

        /// <summary>
        /// Cache filtering before RemoveKey must bind exactly the remaining items, including an empty final result.
        /// </summary>
        [Fact]
        public void AutoRefreshFilterRemoveKeyUpdate_CollectionUpdated()
        {
            // Arrange: all generated items start below a derived threshold.
            var randomizer = new Randomizer(RemoveKeySeed);
            using var source = new TestSourceCache<Person, string>(static person => person.Key);
            var people = Fakers.Person.Clone()
                .UseSeed(randomizer.Int())
                .Generate(randomizer.Int(3, 8))
                .ToArray();
            var exclusiveAge = people.Max(static person => person.Age) + 1;
            ReadOnlyObservableCollection<Person> collection;
            using var subscription = source.Connect()
                .AutoRefresh(x => x.Age)
                .Filter(person => person.Age < exclusiveAge)
                .ValidateSynchronization()
                .ValidateChangeSets(static person => person.Key)
                .RemoveKey()
                .Bind(out collection)
                .Subscribe();

            // Act: add the initial matching collection.
            source.AddOrUpdate(people);

            // Assert: the complete initial membership is present.
            Assert.Equivalent(people, collection, strict: true);

            // Act: guarantee one removal while other matching items remain.
            people[0].Age = exclusiveAge;

            // Assert: a stale entry must not be accepted as an extra member.
            Assert.Equivalent(people.Where(person => person.Age < exclusiveAge), collection, strict: true);

            // Act: exclude every remaining item.
            foreach (var person in people)
            {
                person.Age = exclusiveAge;
            }

            // Assert: strict comparison also rejects stale items when the expectation is empty.
            Assert.Equivalent(people.Where(person => person.Age < exclusiveAge), collection, strict: true);
        }
    }

}
