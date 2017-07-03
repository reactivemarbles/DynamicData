using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class GroupOnFixture: IDisposable
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<IGroup<Person, int>> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().GroupOn(p => p.Age).AsAggregator();
        }


        public void Dispose()
        {
            _source.Dispose();
        }

        [Test]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

            var firstGroup = _results.Data.Items.First().List.Items.ToArray();
            firstGroup[0].Should().Be(person, "Should be same person");
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);
            _source.Remove(person);
            _results.Messages.Count.Should().Be(2, "Should be 1 updates");
            _results.Data.Count.Should().Be(0, "Should be no groups");
        }

        [Test]
        public void UpdateWillChangeTheGroup()
        {
            var person = new Person("Adult1", 50);
            var amended = new Person("Adult1", 60);
            _source.Add(person);
            _source.ReplaceAt(0, amended);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

            var firstGroup = _results.Data.Items.First().List.Items.ToArray();
            firstGroup[0].Should().Be(amended, "Should be same person");
        }

        [Test]
        public void BigList()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(10000).ToArray();
            _source.AddRange(people);

            Console.WriteLine();
        }
    }
}
