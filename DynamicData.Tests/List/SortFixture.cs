
using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    public class SortFixture: IDisposable
    {
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly ISourceList<Person> _source;
        private readonly ChangeSetAggregator<Person> _results;

        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>
            .Ascending(p => p.Name)
            .ThenByAscending(p => p.Age);

        public  SortFixture()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().Sort(_comparer).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Fact]
        public void Insert()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.Add(shouldbefirst);

            _results.Data.Count.Should().Be(101, "Should be 100 people in the cache");

            _results.Data.Items.First().Should().Be(shouldbefirst);
        }

        [Fact]
        public void Replace()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.ReplaceAt(10, shouldbefirst);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            _results.Data.Items.First().Should().Be(shouldbefirst);
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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
