using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Binding
{

    public class IObservableListBindCacheSortedFixture : IDisposable
    {
        private readonly IObservableList<Person> _list;
        private readonly ChangeSetAggregator<Person> _listNotifications;
        private readonly ISourceCache<Person, string> _source;
        private readonly SortedChangeSetAggregator<Person, string> _sourceCacheNotifications;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

        public IObservableListBindCacheSortedFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _sourceCacheNotifications = _source
                .Connect()
                .AutoRefresh()
                .Sort(_comparer, resetThreshold: 25)
                .BindToObservableList(out _list)
                .AsAggregator();

            _listNotifications = _list.Connect().AsAggregator();
        }

        public void Dispose()
        {
            _sourceCacheNotifications.Dispose();
            _listNotifications.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            _list.Count.Should().Be(1, "Should be 1 item in the collection");
            _list.Items.First().Should().Be(person, "Should be same person");
        }

        [Fact]
        public void UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(personUpdated);

            _list.Count.Should().Be(1, "Should be 1 item in the collection");
            _list.Items.First().Should().Be(personUpdated, "Should be updated person");
        }

        [Fact]
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);
            _source.Remove(person);

            _list.Count.Should().Be(0, "Should be 1 item in the collection");
        }

        [Fact]
        public void BatchAdd()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            _list.Count.Should().Be(100, "Should be 100 items in the collection");
            _list.Should().BeEquivalentTo(_list, "Collections should be equivalent");
        }

        [Fact]
        public void BatchRemove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);
            _source.Clear();
            _list.Count.Should().Be(0, "Should be 100 items in the collection");
        }

        [Fact]
        public void CollectionIsInSortOrder()
        {
            _source.AddOrUpdate(_generator.Take(100));
            var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
            sorted.Should().BeEquivalentTo(_list.Items);
        }

        [Fact]
        public void ListRecievesRefresh()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            person.Age = 60;

            _listNotifications.Messages.Count().Should().Be(2);
            _listNotifications.Messages.Last().First().Reason.Should().Be(ListChangeReason.Refresh);
        }

        [Fact]
	    public void TreatMovesAsRemoveAdd()
	    {
		    var cache = new SourceCache<Person, string>(p => p.Name);

		    var people = Enumerable.Range(0,10).Select(age => new Person("Person" + age, age)).ToList();
		    var importantGuy = people.First();
		    cache.AddOrUpdate(people);

		    ISortedChangeSet<Person, string> latestSetWithoutMoves = null;
		    ISortedChangeSet<Person, string> latestSetWithMoves = null;

		    using (cache.Connect()
			    .AutoRefresh(p => p.Age)
			    .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
			    .TreatMovesAsRemoveAdd()
			    .BindToObservableList(out var boundList1)
			    .Subscribe(set => latestSetWithoutMoves = set))

		    using (cache.Connect()
			    .AutoRefresh(p => p.Age)
			    .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
                .BindToObservableList(out var boundList2)
                .Subscribe(set => latestSetWithMoves = set))
		    {

			    importantGuy.Age = importantGuy.Age + 200;

			    latestSetWithoutMoves.Removes.Should().Be(1);
			    latestSetWithoutMoves.Adds.Should().Be(1);
			    latestSetWithoutMoves.Moves.Should().Be(0);
			    latestSetWithoutMoves.Updates.Should().Be(0);

			    latestSetWithMoves.Moves.Should().Be(1);
			    latestSetWithMoves.Updates.Should().Be(0);
			    latestSetWithMoves.Removes.Should().Be(0);
			    latestSetWithMoves.Adds.Should().Be(0);
		    }
	    }
    }
}
