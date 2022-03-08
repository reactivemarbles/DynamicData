using System;
using System.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class ObservableCollectionBindListFixture : IDisposable
{
    private readonly IDisposable _binder;

    private readonly ObservableCollectionExtended<Person> _collection = new();

    private readonly RandomPersonGenerator _generator = new();

    private readonly SourceList<Person> _source;

    public ObservableCollectionBindListFixture()
    {
        _collection = new ObservableCollectionExtended<Person>();
        _source = new SourceList<Person>();
        _binder = _source.Connect().Bind(_collection).Subscribe();
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

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
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
}
