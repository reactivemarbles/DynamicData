using System;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class SourceCacheFixture
    {
        private ChangeSetAggregator<Person, string> _results;
        private ISourceCache<Person, string> _source;

        [SetUp]
        public void MyTestInitialize()
        {
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = _source.Connect().AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void CanHandleABatchOfUpdates()
        {
            _source.Edit(updater =>
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

            Assert.AreEqual(6, _results.Summary.Overall.Count, "Should be  6 up`dates");
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 message");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 update");
            Assert.AreEqual(3, _results.Messages[0].Updates, "Should be 3 updates");
            Assert.AreEqual(1, _results.Messages[0].Removes, "Should be  1 remove");
            Assert.AreEqual(1, _results.Messages[0].Refreshes, "Should be 1 evaluate");

            Assert.AreEqual(0, _results.Data.Count, "Should be 1 item in` the cache");
        }

        [Test]
        public void CountChangedShouldAlwaysInvokeUponeSubscription()
        {
            int? result = null;
            var subscription = _source.CountChanged
                                      .Subscribe(count => result = count);

            Assert.IsTrue(result.HasValue, "Count has not been invoked. Should start at zero");
            Assert.AreEqual(0, result.Value, "Count should be zero");

            subscription.Dispose();
        }

        [Test]
        public void CountChangedShouldReflectContentsOfCacheInvokeUponeSubscription()
        {
            var generator = new RandomPersonGenerator();
            int? result = null;
            var subscription = _source.CountChanged
                                      .Subscribe(count => result = count);

            _source.AddOrUpdate(generator.Take(100));

            Assert.IsTrue(result.HasValue, "Count has not been invoked. Should start at zero");
            Assert.AreEqual(100, result.Value, "Count should be 100");
            subscription.Dispose();
        }

        [Test]
        public void SubscribesDisposesCorrectly()
        {
            bool called = false;
            bool errored = false;
            bool completed = false;
            IDisposable subscription = _source.Connect()
                                              .FinallySafe(() => completed = true)
                                              .Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
            _source.AddOrUpdate(new Person("Adult1", 40));

            //_stream.
            subscription.Dispose();
            _source.Dispose();

            Assert.IsFalse(errored, "Should be no error");
            Assert.IsTrue(called, "Subscription has not been invoked");
            Assert.IsTrue(completed, "Completed has not been invoked");
        }
    }
}
