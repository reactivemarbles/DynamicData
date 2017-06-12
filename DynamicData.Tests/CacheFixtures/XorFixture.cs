using System;
using System.Collections.Generic;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class XOrFixture : XOrFixtureBase
    {
        protected override IObservable<IChangeSet<Person, string>> CreateObservable()
        {
            return _source1.Connect().Xor(_source2.Connect());
        }
    }

    [TestFixture]
    public class  XOrCollectionFixture : XOrFixtureBase
    {
        protected override IObservable<IChangeSet<Person, string>> CreateObservable()
        {
            var l = new List<IObservable<IChangeSet<Person, string>>> { _source1.Connect(), _source2.Connect() };
            return l.Xor();
        }
    }

    [TestFixture]
    public abstract class XOrFixtureBase
    {
        protected ISourceCache<Person, string> _source1;
        protected ISourceCache<Person, string> _source2;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceCache<Person, string>(p => p.Name);
            _source2 = new SourceCache<Person, string>(p => p.Name);
            _results = CreateObservable().AsAggregator();
        }

        protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();

        [TearDown]
        public void Cleanup()
        {
            _source1.Dispose();
            _source2.Dispose();
            _results.Dispose();
        }

        [Test]
        public void UpdatingOneSourceOnlyProducesResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
        }

        [Test]
        public void UpdatingBothDoeNotProducesResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void RemovingFromOneDoesNotFromResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            Assert.AreEqual(3, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void UpdatingOneProducesOnlyOneUpdate()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            var personUpdated = new Person("Adult1", 51);
            _source2.AddOrUpdate(personUpdated);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }
    }
}
