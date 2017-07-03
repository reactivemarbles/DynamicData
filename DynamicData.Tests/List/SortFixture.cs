
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.List
{
    
    internal class SortFixture
    {
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;

        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>
            .Ascending(p => p.Name)
            .ThenByAscending(p => p.Age);

        [SetUp]
        public void SetUp()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().Sort(_comparer).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void Insert()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.Add(shouldbefirst);

            _results.Data.Count.Should().Be(101, "Should be 100 people in the cache");

            _results.Data.Items.First().Should().Be(shouldbefirst);
        }

        [Test]
        public void Replace()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.ReplaceAt(10, shouldbefirst);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            _results.Data.Items.First().Should().Be(shouldbefirst);
        }

        [Test]
        public void Remove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            var toRemove = people.ElementAt(20);
            people.RemoveAt(20);
            _source.RemoveAt(20);

            _results.Data.Count.Should().Be(99, "Should be 99 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
            _results.Messages[1].First().Item.Current.Should().Be(toRemove, "Incorrect item removed");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void RemoveManyOrdered()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

            _results.Data.Count.Should().Be(10, "Should be 10 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

            var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
            var actualResult = _results.Data.Items;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void RemoveManyReverseOrdered()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

            _results.Data.Count.Should().Be(10, "Should be 99 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

            var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
            var actualResult = _results.Data.Items;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void RemoveManyOdds()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            var odd = people.Select((p, idx) => new {p, idx}).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

            _source.RemoveMany(odd);

            _results.Data.Count.Should().Be(50, "Should be 99 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

            var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
            var actualResult = _results.Data.Items;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }
    }
}
