using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
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

        [TearDown]
        public void Cleanup()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void Insert()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.Add(shouldbefirst);

            Assert.AreEqual(101, _results.Data.Count, "Should be 100 people in the cache");

            Assert.AreEqual(shouldbefirst, _results.Data.Items.First());
        }

        [Test]
        public void Replace()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbefirst = new Person("__A", 99);
            _source.ReplaceAt(10, shouldbefirst);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            Assert.AreEqual(shouldbefirst, _results.Data.Items.First());
        }

        [Test]
        public void Remove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            var toRemove = people.ElementAt(20);
            people.RemoveAt(20);
            _source.RemoveAt(20);

            Assert.AreEqual(99, _results.Data.Count, "Should be 99 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");
            Assert.AreEqual(toRemove, _results.Messages[1].First().Item.Current, "Incorrect item removed");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void RemoveManyOrdered()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

            Assert.AreEqual(10, _results.Data.Count, "Should be 99 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");

            var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void RemoveManyReverseOrdered()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

            Assert.AreEqual(10, _results.Data.Count, "Should be 99 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");

            var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void RemoveManyOdds()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            var odd = people.Select((p, idx) => new { p, idx }).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

            _source.RemoveMany(odd);

            Assert.AreEqual(50, _results.Data.Count, "Should be 99 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");

            var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }
    }
}
