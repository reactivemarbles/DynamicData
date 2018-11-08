using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using DynamicData.Binding;
using DynamicData.ReactiveUI.Tests.Domain;
using FluentAssertions;
using ReactiveUI;
using ReactiveUI.Legacy;
using Xunit;

#pragma warning disable CS0618 // Using legacy code.

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
    
    public class BindSortedChangeSetFixture: IDisposable
    {
        private readonly ReactiveList<Person> _collection;
        private readonly ISourceCache<Person, string> _source;
        private readonly IDisposable _binder;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

        public BindSortedChangeSetFixture()
        {
            _collection = new ReactiveList<Person>();
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect()
                .Sort(_comparer, resetThreshold: 25)
                .Bind(_collection)
                .Subscribe();

        }

        public void Dispose()
        {
            _binder.Dispose();
            _source.Dispose();
        }


        [Fact]
        public void AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(person, "Should be same person");
        }

        [Fact]
        public void UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(personUpdated);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(personUpdated, "Should be updated person");
        }

        [Fact]
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);
            _source.Remove(person);

            _collection.Count.Should().Be(0, "Should be 1 item in the collection");
        }

        [Fact]
        public void BatchAdd()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            _collection.Count.Should().Be(100, "Should be 100 items in the collection");
            _collection.ShouldAllBeEquivalentTo(_collection, "Collections should be equivalent");
        }

        [Fact]
        public void BatchRemove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);
            _source.Clear();
            _collection.Count.Should().Be(0, "Should be 100 items in the collection");
        }

        [Fact]
        public void CollectionIsInSortOrder()
        {
            _source.AddOrUpdate(_generator.Take(100));
            var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
            sorted.ShouldAllBeEquivalentTo(_collection.ToList());
        }

        [Fact]
        public void LargeUpdateInvokesAReset()
        {
            //update once as intital load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            bool invoked = false;
            _collection.CollectionChanged += (sender, e) =>
            {
                invoked = true;
                e.Action.Should().Be(NotifyCollectionChangedAction.Reset);
            };
            _source.AddOrUpdate(_generator.Take(100));

            invoked.Should().BeTrue();
        }

        [Fact]
        public void SmallChangeDoesNotInvokeReset()
        {
            //update once as intital load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            bool invoked = false;
            bool resetinvoked = false;
            _collection.CollectionChanged += (sender, e) =>
            {
                invoked = true;
                if (e.Action == NotifyCollectionChangedAction.Reset)
                    resetinvoked = true;
            };
            _source.AddOrUpdate(_generator.Take(24));

            invoked.Should().BeTrue();
            resetinvoked.Should().BeFalse();
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

		    var boundList1 = new ReactiveList<Person>();
		    var boundList2 = new ReactiveList<Person>();


		    using (cache.Connect()
			    .AutoRefresh(p => p.Age)
			    .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
			    .TreatMovesAsRemoveAdd()
			    .Bind(boundList1)
			    .Subscribe(set => latestSetWithoutMoves = set))

		    using (cache.Connect()
			    .AutoRefresh(p => p.Age)
			    .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
			    .Bind(boundList2)
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