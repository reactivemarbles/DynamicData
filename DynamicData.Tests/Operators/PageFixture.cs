using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Controllers;
using DynamicData.Operators;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    public class PageFixture
    {
        private ISourceCache<Person, string> _source;
        private PagedChangeSetAggregator<Person, string> _aggregators;

        private PageController _pageController;
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

            _source = new SourceCache<Person, string>(p=>p.Key);
            _pageController = new PageController(new PageRequest(1,25));
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
            _source.BatchUpdate(updater => updater.AddOrUpdate(people));

            Assert.AreEqual(25, _aggregators.Data.Count, "Should be 25 people in the cache");
            Assert.AreEqual(25, _aggregators.Messages[0].Response.PageSize,"Page size should be 25");
            Assert.AreEqual(1, _aggregators.Messages[0].Response.Page,"Should be page 1");
            Assert.AreEqual(4, _aggregators.Messages[0].Response.Pages, "Should be page 4 pages");

            var expectedResult = people.OrderBy(p => p, _comparer).Take(25).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[0].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
        }


        [Test]
        public void ChangePage()
        {
            var people = _generator.Take(100).ToArray();
            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _pageController.Change(new PageRequest(2,25));



            var expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(25).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();
            CollectionAssert.AreEquivalent(expectedResult, actualResult);
       }

        [Test]
        public void ChangePageSize()
        {
            var people = _generator.Take(100).ToArray();
            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _pageController.Change(new PageRequest(1, 50));

            Assert.AreEqual(1, _aggregators.Messages[1].Response.Page, "Should be page 1");
        

            var expectedResult = people.OrderBy(p => p, _comparer).Take(50).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);


        }


        [Test]
        public void PageGreaterThanNumberOfPagesAvailable()
        {
            var people = _generator.Take(100).ToArray();
            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _pageController.Change(new PageRequest(10, 25));


            Assert.AreEqual(4, _aggregators.Messages[1].Response.Page, "Page should move to the last page");
      

            var expectedResult = people.OrderBy(p => p, _comparer).Skip(75).Take(25).Select(p => new KeyValuePair<string,Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            CollectionAssert.AreEquivalent(expectedResult, actualResult);
      }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowsForNullParameters()
        {
            _pageController.Change(null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowsForNegativeSizeParameters()
        {
            _pageController.Change(new PageRequest(1,-1));
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowsForNegativePage()
        {
            _pageController.Change(new PageRequest(-1, 1));
        }
    }
}