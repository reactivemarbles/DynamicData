using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{


    [TestFixture]
    public class AndFixture
    {
        private ISourceCache<Person, string> _source1;
        private ISourceCache<Person, string> _source2;
        private ChangeSetAggregator<Person, string> _results;
        
        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceCache<Person, string>(p => p.Name);
            _source2 = new SourceCache<Person, string>(p => p.Name);
            _results = _source1.Connect().And(_source2.Connect()).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source1.Dispose();
            _source2.Dispose();
            _results.Dispose();
        }

        [Test]
        public void UpdatingOneSourceOnlyProducesNoResults()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);

            Assert.AreEqual(0, _results.Messages.Count, "Should have no updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }


        [Test]
        public void UpdatingBothProducesResults()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);
            Assert.AreEqual(1, _results.Messages.Count, "Should have no updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
             Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }


        [Test]
        public void RemovingFromOneRemovesFromResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
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
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
            Assert.AreEqual(personUpdated, _results.Data.Items.First(), "Should be updated person");
        }
    }
}