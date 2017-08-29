using System;
using System.Linq;
using DynamicData.ReactiveUI.Tests.Domain;
using FluentAssertions;
using ReactiveUI;
using Xunit;

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
    
    public class BindChangeSetFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _source;
        private readonly IDisposable _binder;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly ReactiveList<Person> _collection;


        public BindChangeSetFixture()
        {
            _collection = new ReactiveList<Person>();
            _source = new SourceCache<Person, string>(p => p.Name);
            _binder = _source.Connect().Bind(_collection).Subscribe();
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

    }
}
