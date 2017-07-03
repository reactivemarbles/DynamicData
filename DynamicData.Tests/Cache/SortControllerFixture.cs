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
    
    public class SortControllerFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _cache;
        private readonly SortController<Person> _sortController;
        private readonly SortedChangeSetAggregator<Person, string> _results;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _comparer;


        public  SortControllerFixture()
        {
            _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

            _cache = new SourceCache<Person, string>(p => p.Name);
            _sortController = new SortController<Person>(_comparer);

            _results = new SortedChangeSetAggregator<Person, string>
                (
                _cache.Connect().Sort(_sortController)
                );
        }

        public void Dispose()
        {
            _cache.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Fact]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

            _sortController.Change(desc);
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Fact]
        public void InlineChanges()
        {
            var people = _generator.Take(10000).ToArray();
            _cache.AddOrUpdate(people);

            //apply mutable changes to the items
            var random = new Random();
            var tochange = people.OrderBy(x => Guid.NewGuid()).Take(10).ToList();

            tochange.ForEach(p => p.Age = random.Next(0, 100));

            // _sortController.Resort();

            _cache.Refresh(tochange);

            var expected = people.OrderBy(t => t, _comparer).ToList();
            var actual = _results.Messages.Last().SortedItems.Select(kv => kv.Value).ToList();
            actual.ShouldAllBeEquivalentTo(expected);

            var list = new ObservableCollectionExtended<Person>();
            var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
            foreach (var message in _results.Messages)
            {
                adaptor.Adapt(message, list);
            }
            list.ShouldAllBeEquivalentTo(expected);
        }

        [Fact]
        public void Reset()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).OrderBy(x => Guid.NewGuid()).ToArray();
            _cache.AddOrUpdate(people);
            _sortController.Change(SortExpressionComparer<Person>.Descending(p => p.Age));
            _sortController.Reset();

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[2].SortedItems.ToList();
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }
    }
}
