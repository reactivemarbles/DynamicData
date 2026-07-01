using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;
using DynamicData.Tests.Domain;
using System.Collections.ObjectModel;
using System.Linq;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public sealed class Static
        : Base
    {
        [Fact]
        public void FilterIsNull_ThrowsException()
            => FluentActions.Invoking(static () => ObservableCacheEx.Filter(
                    source:     Observable.Empty<IChangeSet<Item, int>>(),
                    filter:     null!))
                .Should()
                .Throw<ArgumentNullException>();

        [Theory]
        [InlineData(CompletionStrategy.Asynchronous)]
        [InlineData(CompletionStrategy.Immediate)]
        public void SourceCompletes_CompletionPropagates(CompletionStrategy completionStrategy)
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);


            // UUT Initialization & Action
            if (completionStrategy is CompletionStrategy.Immediate)
                source.Complete();

            using var subscription = source.Connect(suppressEmptyChangeSets: false)
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            if (completionStrategy is CompletionStrategy.Asynchronous)
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
        [Fact]
        public void AutoRefreshRemoveKeyFilterUpdate_CollectionUpdated()
        {
            RandomPersonGenerator generator = new();
            using var source = new SourceCache<Person, string>(p => p.Key);
            var people = generator.Take(100).ToArray();
            var average = people.Average(x => x.Age);
            ReadOnlyObservableCollection<Person> collection;
            using var subscription = source.Connect()
                .AutoRefresh(x => x.Age)
                .RemoveKey()
                .Filter(x => x.Age < average)
                .Bind(out collection)
                .Subscribe();
            source.AddOrUpdate(people);

            Assert.Equivalent(people.Where(x => x.Age < average), collection);

            foreach (var person in people)
            {
                person.Age = person.Age + 1;
            }
            Assert.Equivalent(people.Where(x => x.Age < average), collection);
        }

        [Fact]
        public void AutoRefreshFilterRemoveKeyUpdate_CollectionUpdated()
        {
            RandomPersonGenerator generator = new();
            using var source = new SourceCache<Person, string>(p => p.Key);
            var people = generator.Take(100).ToArray();
            var average = people.Average(x => x.Age);
            ReadOnlyObservableCollection<Person> collection;
            using var subscription = source.Connect()
                .AutoRefresh(x => x.Age)
                .Filter(x => x.Age < average)
                .RemoveKey()
                .Bind(out collection)
                .Subscribe();
            source.AddOrUpdate(people);

            Assert.Equivalent(people.Where(x => x.Age < average), collection);

            foreach (var person in people)
            {
                person.Age = person.Age + 1;
            }
            Assert.Equivalent(people.Where(x => x.Age < average), collection);
        }
    }

}
