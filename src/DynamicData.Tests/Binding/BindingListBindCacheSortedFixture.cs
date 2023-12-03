#if SUPPORTS_BINDINGLIST

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding
{
    public class BindingListBindCacheSortedFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection;

        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

        private readonly RandomPersonGenerator _generator = new();

        private readonly ISourceCache<Person, string> _source;

        public BindingListBindCacheSortedFixture()
        {
            _collection = new BindingList<Person>();
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect().Sort(_comparer, resetThreshold: 25).Bind(_collection).Subscribe();
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
        public void BatchAdd()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            _collection.Count.Should().Be(100, "Should be 100 items in the collection");
            _collection.Should().BeEquivalentTo(_collection, "Collections should be equivalent");
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
            sorted.Should().BeEquivalentTo(_collection.ToList());
        }


        [Fact]
        public void LargeUpdateInvokesAReset()
        {
            //update once as initial load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            var invoked = false;
            _collection.ListChanged += (sender, e) =>
                {
                    invoked = true;
                    e.ListChangedType.Should().Be(ListChangedType.Reset);
                };
            _source.AddOrUpdate(_generator.Take(100));

            invoked.Should().BeTrue();
        }

        [Fact]
        public void Refresh()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            ListChangedEventArgs? args = null;

            _collection.ListChanged += (_, e) =>
            {
                args = e;
            };

            _source.Refresh(people[10]);

            args.Should().NotBeNull();
            args.ListChangedType.Should().Be(ListChangedType.ItemChanged);

            _collection[args.NewIndex].Should().Be(people[10]);
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
        public void SmallChangeDoesNotInvokeReset()
        {
            //update once as initial load is always a reset
            _source.AddOrUpdate(new Person("Me", 21));

            var invoked = false;
            var resetInvoked = false;
            _collection.ListChanged += (sender, e) =>
                {
                    invoked = true;
                    if (e.ListChangedType == ListChangedType.Reset)
                    {
                        resetInvoked = true;
                    }
                };
            _source.AddOrUpdate(_generator.Take(24));

            invoked.Should().BeTrue();
            resetInvoked.Should().BeFalse();
        }

        [Fact]
        public void TreatMovesAsRemoveAdd()
        {
            var cache = new SourceCache<Person, string>(p => p.Name);

            var people = Enumerable.Range(0, 10).Select(age => new Person("Person" + age, age)).ToList();
            var importantGuy = people.First();
            cache.AddOrUpdate(people);

            ISortedChangeSet<Person, string>? latestSetWithoutMoves = null;
            ISortedChangeSet<Person, string>? latestSetWithMoves = null;

            var boundList1 = new ObservableCollectionExtended<Person>();
            var boundList2 = new ObservableCollectionExtended<Person>();

            using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).TreatMovesAsRemoveAdd().Bind(boundList1).Subscribe(set => latestSetWithoutMoves = set))

            using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Bind(boundList2).Subscribe(set => latestSetWithMoves = set))
            {
                if (latestSetWithoutMoves is null)
                {
                    throw new InvalidOperationException(nameof(latestSetWithoutMoves));
                }

                if (latestSetWithMoves is null)
                {
                    throw new InvalidOperationException(nameof(latestSetWithMoves));
                }

                importantGuy.Age += 200;
                latestSetWithoutMoves.Should().NotBeNull();
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

        
        public void Dispose()
        {
            _binder.Dispose();
            _source.Dispose();
        }
    }


}
#endif
