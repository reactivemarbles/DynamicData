using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    
    public class BindSortedChangeSetFixture
    {
        private ObservableCollectionExtended<Person> _collection = new ObservableCollectionExtended<Person>();
        private ISourceCache<Person, string> _source;
        private IDisposable _binder;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

        [SetUp]
        public void SetUp()
        {
            _collection = new ObservableCollectionExtended<Person>();
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

        [Test]
        public void AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(person, "Should be same person");
        }

        [Test]
        public void UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(personUpdated);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(personUpdated, "Should be updated person");
        }

        [Test]
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);
            _source.Remove(person);

            _collection.Count.Should().Be(0, "Should be 1 item in the collection");
        }

        [Test]
        public void BatchAdd()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);

            _collection.Count.Should().Be(100, "Should be 100 items in the collection");
            _collection.ShouldAllBeEquivalentTo(_collection, "Collections should be equivalent");
        }

        [Test]
        public void BatchRemove()
        {
            var people = _generator.Take(100).ToList();
            _source.AddOrUpdate(people);
            _source.Clear();
            _collection.Count.Should().Be(0, "Should be 100 items in the collection");
        }

        [Test]
        public void CollectionIsInSortOrder()
        {
            _source.AddOrUpdate(_generator.Take(100));
            var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
            sorted.ShouldAllBeEquivalentTo(_collection.ToList());
        }

        [Test]
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

        [Test]
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
    }
}
