using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    [TestFixture]
    public class DynamicExceptFixture
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
            _results = _source.Except().AsAggregator();
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
        public void DoNotIncludeExceptListItems()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source2.AddOrUpdate(person);
            _source1.AddOrUpdate(person);

            _results.Messages.Count.Should().Be(0, "Should have no updates");
            _results.Data.Count.Should().Be(0, "Cache should have no items");
        }

        [Test]
        public void RemovedAnItemFromExceptThenIncludesTheItem()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            var person = new Person("Adult1", 50);
            _source2.AddOrUpdate(person);
            _source1.AddOrUpdate(person);

            _source2.Remove(person);
            _results.Messages.Count.Should().Be(1, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Cache should have no items");
        }

        [Test]
        public void AddAndRemoveLists()
        {
            var items = _generator.Take(100).OrderBy(p => p.Name).ToArray();

            _source1.AddOrUpdate(items);
            _source2.AddOrUpdate(items.Take(10));
            _source3.AddOrUpdate(items.Skip(90).Take(10));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            _results.Data.Count.Should().Be(80);
            _results.Data.Items.ShouldAllBeEquivalentTo(items.Skip(10).Take(80));

            _source.RemoveAt(2);
            _results.Data.Count.Should().Be(90);
            _results.Data.Items.ShouldAllBeEquivalentTo(items.Skip(10));

            _source.RemoveAt(0);
            _results.Data.Count.Should().Be(10);
            _results.Data.Items.ShouldAllBeEquivalentTo(items.Take(10));
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
