using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public sealed class Static
        : Base
    {
        private const int AutoRefreshSeed = 0x1165;

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
        /// Cache filtering must bind exactly the matching items, so an item that stops matching
        /// is removed rather than retained as a stale extra entry.
        /// </summary>
        [Fact]
        public void AutoRefreshFilterUpdate_CollectionUpdated()
        {
            // Setup: every generated item starts below a derived threshold, so all of them match initially.
            var randomizer = new Randomizer(AutoRefreshSeed);
            using var source = new TestSourceCache<Person, string>(static person => person.Key);
            var people = Fakers.Person.Clone()
                .UseSeed(randomizer.Int())
                .Generate(randomizer.Int(3, 8))
                .ToArray();
            var exclusiveAge = people.Max(static person => person.Age) + 1;


            // UUT Initialization
            using var subscription = source.Connect()
                .AutoRefresh(static person => person.Age)
                .Filter(person => person.Age < exclusiveAge)
                .ValidateSynchronization()
                .ValidateChangeSets(static person => person.Key)
                .Bind(out ReadOnlyObservableCollection<Person> collection)
                .Subscribe();


            // UUT Action
            source.AddOrUpdate(people);

            collection.Should().BeEquivalentTo(people, "every item matches the predicate");


            // UUT Action
            people[0].Age = exclusiveAge;

            collection.Should().BeEquivalentTo(people.Where(person => person.Age < exclusiveAge),
                "an item that stops matching must be removed, not retained as a stale extra entry");


            // UUT Action
            foreach (var person in people)
            {
                person.Age = exclusiveAge;
            }

            collection.Should().BeEmpty("no item matches the predicate any longer");
        }
    }

}
