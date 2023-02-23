#if SUPPORTS_BINDINGLIST

using System;
using System.ComponentModel;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding
{
    public class BindingLIstBindListFixture : IDisposable
    {
        private readonly IDisposable _binder;

        private readonly BindingList<Person> _collection;

        private readonly RandomPersonGenerator _generator = new();

        private readonly SourceList<Person> _source;

        public BindingLIstBindListFixture()
        {
            _collection = new BindingList<Person>();
            _source = new SourceList<Person>();
            _binder = _source.Connect()
                .AutoRefresh(p => p.Age)
                .Bind(_collection)
                .Subscribe();
        }

        [Fact]
        public void AddRange()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _collection.Count.Should().Be(100, "Should be 100 items in the collection");
            _collection.Should().BeEquivalentTo(_collection, "Collections should be equivalent");
        }

        [Fact]
        public void AddToSourceAddsToDestination()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(person, "Should be same person");
        }

        [Fact]
        public void Clear()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);
            _source.Clear();
            _collection.Count.Should().Be(0, "Should be 100 items in the collection");
        }


        
        [Fact]
        public void Refresh()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            ListChangedEventArgs? args = null;

            _collection.ListChanged += (_, e) =>
            {
                args = e;
            };

            people[10].Age = 100;

            args.Should().NotBeNull();
            args.ListChangedType.Should().Be(ListChangedType.ItemChanged);
            args.NewIndex.Should().Be(10);
        }


        [Fact]
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);
            _source.Remove(person);

            _collection.Count.Should().Be(0, "Should be 1 item in the collection");
        }

        [Fact]
        public void UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.Add(person);
            _source.Replace(person, personUpdated);

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
