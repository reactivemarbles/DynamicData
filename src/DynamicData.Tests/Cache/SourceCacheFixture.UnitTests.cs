using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.Cache;

public static partial class SourceCacheFixture
{
    public class UnitTests
    {
        [Fact]
        public void CanHandleABatchOfUpdates()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);
            using var results = source.Connect().AsAggregator();

            source.Edit(
                updater =>
                {
                    var torequery = new Person("Adult1", 44);

                    updater.AddOrUpdate(new Person("Adult1", 40));
                    updater.AddOrUpdate(new Person("Adult1", 41));
                    updater.AddOrUpdate(new Person("Adult1", 42));
                    updater.AddOrUpdate(new Person("Adult1", 43));
                    updater.Refresh(torequery);
                    updater.Remove(torequery);
                    updater.Refresh(torequery);
                });

            results.Summary.Overall.Count.Should().Be(6, "Should be  6 up`dates");
            results.Messages.Count.Should().Be(1, "Should be 1 message");
            results.Messages[0].Adds.Should().Be(1, "Should be 1 update");
            results.Messages[0].Updates.Should().Be(3, "Should be 3 updates");
            results.Messages[0].Removes.Should().Be(1, "Should be  1 remove");
            results.Messages[0].Refreshes.Should().Be(1, "Should be 1 evaluate");

            results.Data.Count.Should().Be(0, "Should be 1 item in` the cache");
        }

        [Fact]
        public void ConnectContinuesToWorkNormallyAfterAFailedEdit()
        {
            using var source = new SourceCache<int, int>(static item => item);
    
            source.AddOrUpdate(1);

            source.Invoking(source => source.Edit(_ => throw new Exception("Test")))
                .Should().Throw<Exception>()
                .WithMessage("Test");

            using var subscription = source.Connect().RecordCacheItems(out var results);

            results.Error.Should().BeNull("new subscribers should not receive previous errors");
            results.RecordedChangeSets.Should().ContainSingle("an initial changeset should have been published.");
        }

        // Covers https://github.com/reactivemarbles/DynamicData/issues/1129
        [Fact]
        public void ConnectDuringEditsDoesNotDuplicate()
        {
            using var items = new SourceCache<int, int>(static item => item);
            
            using var subscriptions = new CompositeDisposable();
            
            // An initial subscription is required to initiate internal buffering of changes, during the upcoming .Edit().
            // That is, we want there to be changes buffered, internally, when the mid-edit subscription comes in, to
            // ensure that they don't get duplicated. This is the scenario that came in up #1129. 
            subscriptions.Add(items
                .Connect()
                .Subscribe());
                
            CacheItemRecordingObserver<int, int>? results = null;
                
            items.Edit(inner =>
            {
                inner.AddOrUpdate(1);

                subscriptions.Add(items
                    .Connect()
                    .ValidateChangeSets(static item => item)
                    .RecordCacheItems(out results));
            
                results.Error.Should().BeNull("no errors should have occurred");
                results.RecordedChangeSets.Should().BeEmpty("no changes should be published in the middle of an edit");
            
                inner.AddOrUpdate(2);

                results.Error.Should().BeNull("no errors should have occurred");
                results.RecordedChangeSets.Should().BeEmpty("no changes should be published in the middle of an edit");

                // Explicitly doing a nested edit, as that system is closely intertwined with the edit-tracking system that
                // .Connect() uses.
                items.Remove(item: 1);

                results.Error.Should().BeNull("no errors should have occurred");
                results.RecordedChangeSets.Should().BeEmpty("no changes should be published in the middle of an edit");
            });
            
            results.Should().NotBeNull("the edit delegate should have been invoked");
            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedChangeSets.Should().ContainSingle("subscribers should only receive a single initial changeset");
            results.RecordedItemsByKey.Should().BeEquivalentTo(
                new Dictionary<int, int>() { [2] = 2 },
                options => options.WithoutStrictOrdering(),
                "all items in the source should have propagated downstream");

            results.HasCompleted.Should().BeFalse("the source has not yet completed");
        }

        [Fact]
        public void CountChanged()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);
            using var results = source.Connect().AsAggregator();

            var count = 0;
            var invoked = 0;
            using (source.CountChanged.Subscribe(
                       c =>
                       {
                           count = c;
                           invoked++;
                       }))
            {
                invoked.Should().Be(1);
                count.Should().Be(0);

                source.AddOrUpdate(new RandomPersonGenerator().Take(100));
                invoked.Should().Be(2);
                count.Should().Be(100);

                source.Clear();
                invoked.Should().Be(3);
                count.Should().Be(0);
            }
        }

        [Fact]
        public void CountChangedShouldAlwaysInvokeUponSubscription()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);

            int? result = null;
            var subscription = source.CountChanged.Subscribe(count => result = count);

            result.HasValue.Should().BeTrue();

            if (result is null)
            {
                throw new InvalidOperationException(nameof(result));
            }

            result.Value.Should().Be(0, "Count should be zero");

            subscription.Dispose();
        }

        [Fact]
        public void CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);

            var generator = new RandomPersonGenerator();
            int? result = null;
            var subscription = source.CountChanged.Subscribe(count => result = count);

            source.AddOrUpdate(generator.Take(100));

            if (result is null)
            {
                throw new InvalidOperationException(nameof(result));
            }

            result.HasValue.Should().BeTrue();
            result.Value.Should().Be(100, "Count should be 100");
            subscription.Dispose();
        }

        [Fact]
        public void EmptyChanges()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);

            IChangeSet<Person, string>? change = null;

            using var subscription = source.Connect(suppressEmptyChangeSets: false)
                .Subscribe(c=> change = c);

            change.Should().NotBeNull();
            change!.Count.Should().Be(0);
        }

        [Fact]
        public void EmptyChangesWithFilter()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);

            IChangeSet<Person, string>? change = null;

            using var subscription = source.Connect(p=>p.Age == 20, suppressEmptyChangeSets: false)
                .Subscribe(c => change = c);

            change.Should().NotBeNull();
            change!.Count.Should().Be(0);
        }

        [Fact]
        public void StaticFilterRemove()
        {
            var cache = new SourceCache<SomeObject, int>(x => x.Id);
        
            var above5 = cache.Connect(x => x.Value > 5).AsObservableCache();
            var below5 = cache.Connect(x => x.Value <= 5).AsObservableCache();

            cache.AddOrUpdate(Enumerable.Range(1,10).Select(i=> new SomeObject(i,i)));


            above5.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5).Select(i => new SomeObject(i, i)));
            below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(i => new SomeObject(i, i)));

            //should move from above 5 to below 5
            cache.AddOrUpdate(new SomeObject(6,-1));

            above5.Count.Should().Be(4);
            below5.Count.Should().Be(6);


            above5.Items.Should().BeEquivalentTo(Enumerable.Range(7, 4).Select(i => new SomeObject(i, i)));
            below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 6).Select(i => new SomeObject(i, i == 6 ? -1 : i)));
        }

        [Fact]
        public void SubscribeDisposesCorrectly()
        {
            using var source = new SourceCache<Person, string>(p => p.Key);

            var called = false;
            var errored = false;
            var completed = false;
            var subscription = source.Connect().Finally(() => completed = true).Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
            source.AddOrUpdate(new Person("Adult1", 40));

            subscription.Dispose();
            source.Dispose();

            errored.Should().BeFalse();
            called.Should().BeTrue();
            completed.Should().BeTrue();
        }
    }
}
