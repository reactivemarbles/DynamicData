#if SUPPORTS_BINDINGLIST

using System;
using System.ComponentModel;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding
{
    public class BindingListCacheFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection = new();

        private readonly RandomPersonGenerator _generator = new();

        private readonly ISourceCache<Person, string> _source;

        public BindingListCacheFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect().Bind(_collection).Subscribe();
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
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);
            _source.Remove(person);

            _collection.Count.Should().Be(0, "Should be 1 item in the collection");
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
            args.NewIndex.Should().Be(10);
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
