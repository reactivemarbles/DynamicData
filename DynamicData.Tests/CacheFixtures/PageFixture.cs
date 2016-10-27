using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class PageFixture
    {
        private ISourceCache<Person, string> _source;
        private PagedChangeSetAggregator<Person, string> _aggregators;

        private PageController _pageController;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private IComparer<Person> _comparer;

        [SetUp]
        public void Initialise()
        {
            _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

            _source = new SourceCache<Person, string>(p => p.Key);
            _pageController = new PageController(new PageRequest(1, 25));
            _aggregators = new PagedChangeSetAggregator<Person, string>
                (
                _source.Connect()
                       .Sort(_comparer)
                       .Page(_pageController)
                );
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _aggregators.Dispose();
        }

        [Test]
        public void PageInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);

            Assert.AreEqual(25, _aggregators.Data.Count, "Should be 25 people in the cache");
            Assert.AreEqual(25, _aggregators.Messages[0].Response.PageSize, "Page size should be 25");
            Assert.AreEqual(1, _aggregators.Messages[0].Response.Page, "Should be page 1");
            Assert.AreEqual(4, _aggregators.Messages[0].Response.Pages, "Should be page 4 pages");

            var expectedResult = people.OrderBy(p => p, _comparer).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangePage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(2, 25));

            var expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ChangePageSize()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(1, 50));

            Assert.AreEqual(1, _aggregators.Messages[1].Response.Page, "Should be page 1");

            var expectedResult = people.OrderBy(p => p, _comparer).Take(50).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void PageGreaterThanNumberOfPagesAvailable()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(10, 25));

            Assert.AreEqual(4, _aggregators.Messages[1].Response.Page, "Page should move to the last page");

            var expectedResult = people.OrderBy(p => p, _comparer).Skip(75).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }

        [Test]
        public void ThrowsForNullParameters()
        {
            Assert.Throws<ArgumentNullException>(() => _pageController.Change(null));
        }

        [Test]
        public void ThrowsForNegativeSizeParameters()
        {
            Assert.Throws<ArgumentException>(() => _pageController.Change(new PageRequest(1, -1)));
        }

        [Test]
        public void ThrowsForNegativePage()
        {
            Assert.Throws<ArgumentException>(() => _pageController.Change(new PageRequest(-1, 1)));
        }
    }
}
