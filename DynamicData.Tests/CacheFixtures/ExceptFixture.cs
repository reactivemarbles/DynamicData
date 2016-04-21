using System;
using System.Collections.Generic;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class ExceptFixture : ExceptFixtureBase
    {
        protected override IObservable<IChangeSet<Person, string>> CreateObservable()
        {
            return _targetSource.Connect().Except(_exceptSource.Connect());
        }
    }

    [TestFixture]
    public class ExceptCollectionFixture : ExceptFixtureBase
    {
        protected override IObservable<IChangeSet<Person, string>> CreateObservable()
        {
            var l = new List<IObservable<IChangeSet<Person, string>>> { _targetSource.Connect(), _exceptSource.Connect() };
            return l.Except();
        }
    }

    [TestFixture]
    public abstract class ExceptFixtureBase
    {
        protected ISourceCache<Person, string> _targetSource;
        protected ISourceCache<Person, string> _exceptSource;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _targetSource = new SourceCache<Person, string>(p => p.Name);
            _exceptSource = new SourceCache<Person, string>(p => p.Name);
            _results = CreateObservable().AsAggregator();
        }

        protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();

        [TearDown]
        public void Cleanup()
        {
            _targetSource.Dispose();
            _exceptSource.Dispose();
            _results.Dispose();
        }

        [Test]
        public void UpdatingOneSourceOnlyProducesResult()
        {
            var person = new Person("Adult1", 50);
            _targetSource.AddOrUpdate(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
        }

        [Test]
        public void DoNotIncludeExceptListItems()
        {
            var person = new Person("Adult1", 50);
            _exceptSource.AddOrUpdate(person);
            _targetSource.AddOrUpdate(person);

            Assert.AreEqual(0, _results.Messages.Count, "Should have no updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void RemovedAnItemFromExceptThenIncludesTheItem()
        {
            var person = new Person("Adult1", 50);
            _exceptSource.AddOrUpdate(person);
            _targetSource.AddOrUpdate(person);

            _exceptSource.Remove(person);
            Assert.AreEqual(1, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Cache should have no items");
        }
    }
}
