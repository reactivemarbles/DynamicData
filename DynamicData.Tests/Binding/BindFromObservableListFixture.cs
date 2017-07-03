using System;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    
    public class BindFromObservableListFixture
    {
        private ObservableCollectionExtended<Person> _collection = new ObservableCollectionExtended<Person>();
        private SourceList<Person> _source;
        private IDisposable _binder;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void SetUp()
        {
            _collection = new ObservableCollectionExtended<Person>();
            _source = new SourceList<Person>();
            _binder = _source.Connect().Bind(_collection).Subscribe();
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
            _source.Add(person);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(person, "Should be same person");
        }

        [Test]
        public void UpdateToSourceUpdatesTheDestination()
        {
            var person = new Person("Adult1", 50);
            var personUpdated = new Person("Adult1", 51);
            _source.Add(person);
            _source.Replace(person, personUpdated);

            _collection.Count.Should().Be(1, "Should be 1 item in the collection");
            _collection.First().Should().Be(personUpdated, "Should be updated person");
        }

        [Test]
        public void RemoveSourceRemovesFromTheDestination()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);
            _source.Remove(person);

            _collection.Count.Should().Be(0, "Should be 1 item in the collection");
        }

        [Test]
        public void AddRange()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);

            _collection.Count.Should().Be(100, "Should be 100 items in the collection");
            _collection.ShouldAllBeEquivalentTo(_collection, "Collections should be equivalent");
        }

        [Test]
        public void Clear()
        {
            var people = _generator.Take(100).ToList();
            _source.AddRange(people);
            _source.Clear();
            _collection.Count.Should().Be(0, "Should be 100 items in the collection");
        }
    }
}
