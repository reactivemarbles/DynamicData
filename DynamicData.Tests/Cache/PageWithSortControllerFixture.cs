using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class PageWithSortControllerFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _source;
        private readonly PagedChangeSetAggregator<Person, string> _aggregators;
        private readonly PageController _pageController;
        private readonly SortController<Person> _sortController;
        
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _originalComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
        private readonly IComparer<Person> _changedComparer = SortExpressionComparer<Person>.Descending(p => p.Name).ThenByAscending(p => p.Age);


        public  PageWithSortControllerFixture()
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

        public void Dispose()
        {
            _source.Dispose();
            _aggregators.Dispose();
            _sortController.Dispose();
            _pageController.Dispose();
        }

        [Fact]
        public void ChangePage()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(2, 25));

            var expectedResult = people.OrderBy(p => p, _originalComparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Fact]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _source.AddOrUpdate(people);

            _pageController.Change(new PageRequest(2, 25));
            _sortController.Change(_changedComparer);
            //

            var expectedResult = people.OrderBy(p => p, _changedComparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[2].SortedItems.ToList();
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Fact]
        public void PageSizeLargerThanElements()
        {
            var people = _generator.Take(10).ToArray();
            _source.AddOrUpdate(people);
            _pageController.Change(new PageRequest(1, 20));

            _aggregators.Messages[1].Response.Page.Should().Be(1, "Should be page 1");

            var expectedResult = people.OrderBy(p => p, _originalComparer).Take(10).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _aggregators.Messages[1].SortedItems.ToList();

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }
    }
}
