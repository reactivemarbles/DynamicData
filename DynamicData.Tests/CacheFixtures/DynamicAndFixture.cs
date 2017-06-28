using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class DynamicAndFixture
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
            _results = _source.And().AsAggregator();
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
        public void UpdatingOneSourceOnlyProducesNoResults()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);

            _results.Messages.Count.Should().Be(0, "Should have no updates");
            _results.Data.Count.Should().Be(0, "Cache should have no items");
        }

        [Test]
        public void UpdatingBothProducesResults()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);
            _results.Messages.Count.Should().Be(1, "Should have no updates");
            _results.Data.Count.Should().Be(1, "Cache should have no items");
            _results.Data.Items.First().Should().Be(person, "Should be same person");
        }

        [Test]
        public void RemovingFromOneRemovesFromResult()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(0, "Cache should have no items");
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
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Cache should have no items");
            _results.Data.Items.First().Should().Be(personUpdated, "Should be updated person");
        }

        [Test]
        public void AddAndRemoveLists()
        {
            var items = _generator.Take(100).ToArray();
            _source1.AddOrUpdate(items.Take(20));
            _source2.AddOrUpdate(items.Skip(10).Take(10));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            _results.Data.Count.Should().Be(10);
            _results.Data.Items.ShouldAllBeEquivalentTo(items.Skip(10).Take(10));

            _source.Add(_source3.Connect());
            _results.Data.Count.Should().Be(0);

            _source.RemoveAt(2);
            _results.Data.Count.Should().Be(10);
        }

        [Test]
        public void RemoveAllLists()
        {
            var items = _generator.Take(100).ToArray();

            _source1.AddOrUpdate(items.Take(10));
            _source2.AddOrUpdate(items.Skip(20).Take(10));
            _source3.AddOrUpdate(items.Skip(30));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            _source.RemoveAt(2);
            _source.RemoveAt(1);
            _source.RemoveAt(0);
            //s _source.Clear();

            _results.Data.Count.Should().Be(0);
        }
    }
}
