using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class DynamicOrFixture
    {
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        private ISourceCache<Person, string> _source1;
        private ISourceCache<Person, string> _source2;
        private ISourceCache<Person, string> _source3;
        private ISourceList<IObservable<IChangeSet<Person, string>>> _source;

        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceCache<Person, string>(p => p.Name);
            _source2 = new SourceCache<Person, string>(p => p.Name);
            _source3 = new SourceCache<Person, string>(p => p.Name);
            _source = new SourceList<IObservable<IChangeSet<Person, string>>>();
            _results = _source.Or().AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source1.Dispose();
            _source2.Dispose();
            _source3.Dispose();
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void UpdatingOneSourceOnlyProducesResult()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
        }


        [Test]
        public void UpdatingBothProducesResultsAndDoesNotDuplicateTheMessage()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);
            Assert.AreEqual(1, _results.Messages.Count, "Should have no updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }


        [Test]
        public void RemovingFromOneDoesNotFromResult()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            Assert.AreEqual(1, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void UpdatingOneProducesOnlyOneUpdate()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);


            var personUpdated = new Person("Adult1", 51);
            _source2.AddOrUpdate(personUpdated);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
            Assert.AreEqual(personUpdated, _results.Data.Items.First(), "Should be updated person");
        }

        [Test]
        public void AddAndRemoveLists()
        {
            var items = _generator.Take(100).ToArray();

            _source1.AddOrUpdate(items.Take(10));
            _source2.AddOrUpdate(items.Skip(10).Take(10));
            _source3.AddOrUpdate(items.Skip(20));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());
            
            Assert.AreEqual(100, _results.Data.Count);
            CollectionAssert.AreEquivalent(items, _results.Data.Items);

            _source.RemoveAt(1);
            var result = items.Take(10)
                    .Union(items.Skip(20));


            Assert.AreEqual(90, _results.Data.Count);
            CollectionAssert.AreEquivalent(result, _results.Data.Items);
        }

        [Test]
        public void RemoveAllLists()
        {
            var items = _generator.Take(100).ToArray();

            _source1.AddOrUpdate(items.Take(10));
            _source2.AddOrUpdate(items.Skip(10).Take(10));
            _source3.AddOrUpdate(items.Skip(20));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            _source.Clear();

            Assert.AreEqual(0, _results.Data.Count);
        }

    }
}