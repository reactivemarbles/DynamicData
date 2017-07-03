using System;
using System.Collections.Generic;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.Cache
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

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        }

        [Test]
        public void UpdatingBothDoeNotProducesResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);
            _results.Data.Count.Should().Be(0, "Cache should have no items");
        }

        [Test]
        public void RemovingFromOneDoesNotFromResult()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            _results.Messages.Count.Should().Be(3, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Cache should have no items");
        }

        [Test]
        public void UpdatingOneProducesOnlyOneUpdate()
        {
            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            var personUpdated = new Person("Adult1", 51);
            _source2.AddOrUpdate(personUpdated);
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(0, "Cache should have no items");
        }
    }
}
