using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
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

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        }

        [Test]
        public void UpdatingBothProducesResultsAndDoesNotDuplicateTheMessage()
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
        public void RemovingFromOneDoesNotFromResult()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source1.AddOrUpdate(person);
            _source2.AddOrUpdate(person);

            _source2.Remove(person);
            _results.Messages.Count.Should().Be(1, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Cache should have no items");
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

            _source1.AddOrUpdate(items.Take(10));
            _source2.AddOrUpdate(items.Skip(10).Take(10));
            _source3.AddOrUpdate(items.Skip(20));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            _results.Data.Count.Should().Be(100);
            _results.Data.Items.ShouldAllBeEquivalentTo(items);

            _source.RemoveAt(1);
            var result = items.Take(10)
                .Union(items.Skip(20));

            _results.Data.Count.Should().Be(90);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);
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

            _results.Data.Count.Should().Be(0);
        }
    }
}
