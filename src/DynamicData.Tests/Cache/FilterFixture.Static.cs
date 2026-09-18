using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    [InheritsTests]
    public sealed class Static
        : Base
    {
        [Test]
        public async Task FilterIsNull_ThrowsException()
            => await Assert.That(() => ObservableCacheEx.Filter(
                    source: Observable.Empty<IChangeSet<Item, int>>(),
                    filter: null!)).Throws<ArgumentNullException>();

        [Test]
        [Arguments(StreamCompletionStrategy.Asynchronous)]
        [Arguments(StreamCompletionStrategy.Immediate)]
        public async Task SourceCompletes_CompletionPropagates(StreamCompletionStrategy completionStrategy)
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");

            // Final verification
            await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
        }

        [Test]
        public async Task SubscriptionIsDisposed_SubscriptionDisposalPropagates()
        {
            // Setup
            using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();

            // UUT Intialization
            using var subscription = source
                .Filter(Item.FilterByIsIncluded)
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no source operations were performed");
            await Assert.That(results.RecordedItemsByKey.Values).IsEmpty().Because("the source has not initialized");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            subscription.Dispose();

            await Assert.That(source.HasObservers).IsFalse().Because("subscription disposal should propagate to all sources");
        }

        protected override IObservable<IChangeSet<Item, int>> BuildUut(
                IObservable<IChangeSet<Item, int>> source,
                Func<Item, bool> predicate,
                bool suppressEmptyChangeSets)
            => source.Filter(
                filter: predicate,
                suppressEmptyChangeSets: suppressEmptyChangeSets);
        [Test]
        public async Task AutoRefreshRemoveKeyFilterUpdate_CollectionUpdated()
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

            await Assert.That(collection).IsEquivalentTo(people.Where(x => x.Age < average));

            foreach (var person in people)
            {
                person.Age = person.Age + 1;
            }
            await Assert.That(collection).IsEquivalentTo(people.Where(x => x.Age < average));
        }

        [Test]
        public async Task AutoRefreshFilterRemoveKeyUpdate_CollectionUpdated()
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

            await Assert.That(collection).IsEquivalentTo(people.Where(x => x.Age < average));

            foreach (var person in people)
            {
                person.Age = person.Age + 1;
            }
            await Assert.That(collection).IsEquivalentTo(people.Where(x => x.Age < average));
        }
    }

}
