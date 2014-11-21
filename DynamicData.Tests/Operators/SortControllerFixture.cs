using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class SortControllerFixture
    {
        private ISourceCache<Person, string> _cache;
        private SortController<Person> _sortController;
        private TestSortedChangeSetResult<Person, string> _results;

        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private SortExpressionComparer<Person> _comparer;

        [SetUp]
        public void Initialise()
        {
            _comparer = new SortExpressionComparer<Person>
            {
                new SortExpression<Person>(p => p.Age),
                new SortExpression<Person>(p => p.Name)
            };
            _cache = new SourceCache<Person, string>(p => p.Name);
            _sortController = new SortController<Person>(_comparer);
            
            _results = new TestSortedChangeSetResult<Person, string>
                (
                    _cache.Connect().Sort(_sortController)
                );
        }

        [TearDown]
        public void Cleanup()
        {
            _cache.Dispose();
            _results.Dispose();
        }

        [Test]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            Assert.AreEqual(100, _results.Data.Count, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _cache.BatchUpdate(updater => updater.AddOrUpdate(people));
            
            _sortController.Change(new SortExpressionComparer<Person>{new SortExpression<Person>(p => p.Age,SortDirection.Descending)});
            var desc = new SortExpressionComparer<Person>
            {
                new SortExpression<Person>(p => p.Age,SortDirection.Descending),
                new SortExpression<Person>(p => p.Name)
            };
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }


        [Test]
        public void InlineChanges()
        {
            var people = _generator.Take(10000).ToArray();
            _cache.BatchUpdate(updater => updater.AddOrUpdate(people));
          


            //apply mutable changes to the items
            var random = new Random();
            var tochange = people.OrderBy(x => Guid.NewGuid()).Take(10).ToList();

            tochange.ForEach(p => p.Age = random.Next(0, 100));

           // _sortController.Resort();

           _cache.Evaluate(tochange);

            var expected = people.OrderBy(t=>t,_comparer).ToList();
            var actual = _results.Messages.Last().SortedItems.Select(kv=>kv.Value).ToList();
            CollectionAssert.AreEqual(expected,actual);


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


            _sortController.Change(new SortExpressionComparer<Person> { new SortExpression<Person>(p => p.Age, SortDirection.Descending) });
            _sortController.Reset();

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }
    }
}