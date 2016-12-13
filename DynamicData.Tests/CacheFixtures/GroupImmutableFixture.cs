using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Cache;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class GroupImmutableFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<IGrouping<Person, string, int>, int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = _source.Connect().GroupOnImmutable(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }



        [Test]
        public void Add()
        {

            _source.AddOrUpdate(new Person("Person1", 20));
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 add");
            Assert.AreEqual(1, _results.Messages.First().Adds);
        }

        [Test]
        public void UpdatesArePermissible()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person2", 20));

            Assert.AreEqual(1, _results.Data.Count);//1 group
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Updates);

            var group = _results.Data.Items.First();
            Assert.AreEqual(2, group.Count);
        }

        [Test]
        public void UpdateAnItemWillChangedThegroup()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person1", 21));

            Assert.AreEqual(1, _results.Data.Count);
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes);
            var group = _results.Data.Items.First();
            Assert.AreEqual(1, group.Count);

            Assert.AreEqual(21, group.Key);
        }

        [Test]
        public void Remove()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.Remove(new Person("Person1", 20));

            Assert.AreEqual(2, _results.Messages.Count);
            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void FiresManyValueForBatchOfDifferentAdds()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

            Assert.AreEqual(4, _results.Data.Count);
            Assert.AreEqual(1, _results.Messages.Count);
            Assert.AreEqual(4, _results.Messages.First().Count);
            foreach (var update in _results.Messages.First())
            {
                Assert.AreEqual(ChangeReason.Add, update.Reason);
            }
        }

        [Test]
        public void FiresOnlyOnceForABatchOfUniqueValues()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

            Assert.AreEqual(1, _results.Messages.Count);
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(4, _results.Data.Items.First().Count);
        }
    }
}