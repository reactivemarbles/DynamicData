using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    public class PageWithSortControllerFixture
    {
        private ISourceCache<Person, string> _source;
        private PagedChangeSetAggregator<Person, string> _aggregators;

        private PageController _pageController;
        private SortController<Person> _sortController;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _originalComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
        private readonly IComparer<Person> _changedComparer = SortExpressionComparer<Person>.Descending(p => p.Name).ThenByAscending(p => p.Age);

        [SetUp]
        public void Initialise()
        {
            _sortController = new SortController<Person>(_originalComparer);
            _source = new SourceCache<Person, string>(p => p.Key);
            _pageController = new PageController(new PageRequest(1, 25));
            _aggregators = new PagedChangeSetAggregator<Person, string>
                (
                _source.Connect()
                       .Sort(_sortController)
                       .Page(_pageController)
                );
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _aggregators.Dispose();
            _sortController.Dispose();
            _pageController.Dispose();
        }

        [Test]
        public void ChangePage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(2, 25));

            var expectedResult = people.OrderBy(p => p, _originalComparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);

            _pageController.Change(new PageRequest(2, 25));
            _sortController.Change(_changedComparer);
            //

            var expectedResult = people.OrderBy(p => p, _changedComparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[2].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void PageSizeLargerThanElements()
        {
            var people = _generator.Take(10).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(1, 20));

            Assert.AreEqual(1, _aggregators.Messages[1].Response.Page, "Should be page 1");

            var expectedResult = people.OrderBy(p => p, _originalComparer).Take(10).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }
    }
}
