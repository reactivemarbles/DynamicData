using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class DistinctFixture
    {
        private ISourceCache<Person, string> _source;
        private DistinctChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void FiresAddWhenaNewItemIsAdded()
        {
            _source.AddOrUpdate(new Person("Person1", 20));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void FiresBatchResultOnce()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(3, _results.Data.Count, "Should be 3 items in the cache");

            CollectionAssert.AreEquivalent(new[] { 20, 21, 22 }, _results.Data.Items);
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void DuplicatedResultsResultInNoAdditionalMessage()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 update message");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void RemovingAnItemRemovesTheDistinct()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.Remove(new Person("Person1", 20));
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 update message");
            Assert.AreEqual(0, _results.Data.Count, "Should be 1 items in the cache");

            Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes, "Second messsage should be a remove");
        }

        [Test]
        public void BreakWithLoadsOfUpdates()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person2", 12));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person2", 12));


                updater.AddOrUpdate(new Person("Person3", 13));
                updater.AddOrUpdate(new Person("Person4", 14));
            });

            CollectionAssert.AreEquivalent(new[] { 1, 12, 13, 14 }, _results.Data.Items);

            //This previously threw
            _source.Remove(new Person("Person3", 13));

            CollectionAssert.AreEquivalent(new[] { 1, 12, 14 }, _results.Data.Items);
        }
    }
}
