using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class SortObservableFixtureFixture
    {
        private ISourceCache<Person, string> _cache;
        private SortedChangeSetAggregator<Person, string> _results;

         
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        private BehaviorSubject<IComparer<Person>> _comparerObservable;
        private SortExpressionComparer<Person> _comparer;

        //  private IComparer<Person> _comparer;

        [SetUp]
        public void Initialise()
        {
            _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
            _comparerObservable = new BehaviorSubject<IComparer<Person>>(_comparer);
            _cache = new SourceCache<Person, string>(p => p.Name);
          //  _sortController = new SortController<Person>(_comparer);

            _results = new SortedChangeSetAggregator<Person, string>
            (
                _cache.Connect().Sort(_comparerObservable)
            );
        }

        [TearDown]
        public void Cleanup()
        {
            _cache.Dispose();
            _results.Dispose();
            _comparerObservable.OnCompleted();
        }

        [Test]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

            _comparerObservable.OnNext(desc);
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void InlineChanges()
        {
            var people = _generator.Take(10000).ToArray();
            _cache.AddOrUpdate(people);

            //apply mutable changes to the items
            var random = new Random();
            var tochange = people.OrderBy(x => Guid.NewGuid()).Take(10).ToList();

            tochange.ForEach(p => p.Age = random.Next(0, 100));

            // _sortController.Resort();

            _cache.Evaluate(tochange);

            var expected = people.OrderBy(t => t, _comparer).ToList();
            var actual = _results.Messages.Last().SortedItems.Select(kv => kv.Value).ToList();
            CollectionAssert.AreEqual(expected, actual);

            var list = new ObservableCollectionExtended<Person>();
            var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
            foreach (var message in _results.Messages)
            {
                adaptor.Adapt(message, list);
            }
            CollectionAssert.AreEquivalent(expected, list);
        }

        [Test]
        public void Reset()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).OrderBy(x => Guid.NewGuid()).ToArray();
            _cache.AddOrUpdate(people);
            _comparerObservable.OnNext(SortExpressionComparer<Person>.Descending(p => p.Age));
            _comparerObservable.OnNext(_comparer);

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[2].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }
    }
}