using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class SortMutableFixture
    {
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private ISourceList<Person> _source;
        private ISubject<IComparer<Person>> _changeComparer;
        private ISubject<Unit> _resort;
        private ChangeSetAggregator<Person> _results;


        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>
            .Ascending(p => p.Age)
            .ThenByAscending(p => p.Name);

        [SetUp]
        public void SetUp()
        {
            _source = new SourceList<Person>();
            _changeComparer = new BehaviorSubject<IComparer<Person>>(_comparer);
            _resort = new Subject<Unit>();

            _results = _source.Connect().Sort(_changeComparer, resetThreshold: 25, resort: _resort).AsAggregator();
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

            _results.Data.Count.Should().Be(100);

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void Insert()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbeLast = new Person("__A", 10000);
            _source.Add(shouldbeLast);


            _results.Data.Count.Should().Be(101);

            Assert.AreEqual(shouldbeLast, _results.Data.Items.Last());
        }

        [Test]
        public void Replace()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var shouldbeLast = new Person("__A", 999);
            _source.ReplaceAt(10, shouldbeLast);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            Assert.AreEqual(shouldbeLast, _results.Data.Items.Last());
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
            //Assert.AreEqual(toRemove, _results.Messages[1].First().Item.Current, "Incorrect item removed");

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
            //Assert.AreEqual(toRemove, _results.Messages[1].First().Item.Current, "Incorrect item removed");

            var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ResortOnInlineChanges()
        {
            var people = _generator.Take(10).ToList();
            _source.AddRange(people);


            people[0].Age = -1;
            people[1].Age = -10;
            people[2].Age = -12;
            people[3].Age = -5;
            people[4].Age = -7;
            people[5].Age = -6;


            var comparer = SortExpressionComparer<Person>
                .Descending(p => p.Age)
                .ThenByAscending(p => p.Name);

            _changeComparer.OnNext(comparer);

            //Assert.AreEqual(10, _results.Data.Count, "Should be 99 people in the cache");
            //Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");
            ////Assert.AreEqual(toRemove, _results.Messages[1].First().Item.Current, "Incorrect item removed");

            var expectedResult = people.OrderBy(p => p, comparer).ToArray();
            var actualResult = _results.Data.Items.ToArray();

            //actualResult.(expectedResult);
            CollectionAssert.AreEqual(expectedResult, actualResult);
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
            //Assert.AreEqual(toRemove, _results.Messages[1].First().Item.Current, "Incorrect item removed");

            var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
            var actualResult = _results.Data.Items;
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void Resort()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            people.OrderBy(_ => Guid.NewGuid()).ForEach((person, index) =>
            {
                person.Age = index;
            });

            _resort.OnNext(Unit.Default);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangeComparer()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddRange(people);

            var newComparer = SortExpressionComparer<Person>
                        .Ascending(p => p.Name)
                        .ThenByAscending(p => p.Age);

            _changeComparer.OnNext(newComparer);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, newComparer);
            var actualResult = _results.Data.Items;

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }


        [Test]
        public void UpdateMoreThanThreshold()
        {
            var allPeople = _generator.Take(1100).ToList();
            var people = allPeople.Take(100).ToArray();
            _source.AddRange(people);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var morePeople = allPeople.Skip(100).ToArray();
            _source.AddRange(morePeople);

            Assert.AreEqual(1100, _results.Data.Count, "Should be 1100 people in the cache");
            var expectedResult = people.Union(morePeople).OrderBy(p => p, _comparer).ToArray();
            var actualResult = _results.Data.Items;

            CollectionAssert.AreEquivalent(expectedResult, actualResult);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 messages");

            var lastMessage = _results.Messages.Last();
            Assert.AreEqual(100, lastMessage.First().Range.Count, "Should be 100 in the range");
            Assert.AreEqual(ListChangeReason.Clear, lastMessage.First().Reason);

            Assert.AreEqual(1100, lastMessage.Last().Range.Count, "Should be 1100 in the range");
            Assert.AreEqual(ListChangeReason.AddRange, lastMessage.Last().Reason);
        }
    }
}