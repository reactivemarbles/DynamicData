using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.Cache;

public class OfTypeFixture : IDisposable
{
#if DEBUG
    const int AddCount = 7;
    const int UpdateCount = 5;
    const int RemoveCount = 3;
#else
    const int AddCount = 101;
    const int UpdateCount = 57;
    const int RemoveCount = 53;
#endif

    private readonly Randomizer _randomizer;

    private readonly Faker<Person> _personFaker;

    private readonly Faker<CatPerson> _catPersonFaker;

    private readonly SourceCache<Person, string> _sourceCache = new(p => p.Id);

    private readonly ChangeSetAggregator<Person, string> _personResults;

    private readonly ChangeSetAggregator<CatPerson, string> _catPersonResults;

    public OfTypeFixture(ITestOutputHelper testOutputHelper)
    {
        _randomizer = new(0x3737_ddcc);
        _personFaker = new Faker<Person>().CustomInstantiator(faker => new Person(faker.Person.FullName)).WithSeed(_randomizer);
        _catPersonFaker = new Faker<CatPerson>().CustomInstantiator(faker => new CatPerson(faker.Person.FullName, $"{faker.Hacker.Adjective()} the {faker.Hacker.Noun()}")).WithSeed(_randomizer);
        _personResults = _sourceCache.Connect().TestSpy(testOutputHelper, "Cache").AsAggregator();
        _catPersonResults = _sourceCache.Connect().OfType<Person, string, CatPerson>().TestSpy(testOutputHelper, "OfType").AsAggregator();
    }

    [Fact]
    public void AddedItemsAreInResults()
    {
        // Arrange
        var people = _personFaker.Generate(AddCount);
        var catPeople = _catPersonFaker.Generate(AddCount);

        _sourceCache.AddOrUpdate(people);
        _sourceCache.AddOrUpdate(catPeople);

        _personResults.Summary.Overall.Adds.Should().Be(AddCount * 2);
        _personResults.Messages.Count.Should().Be(2);
        _catPersonResults.Summary.Overall.Adds.Should().Be(AddCount);
        _catPersonResults.Messages.Count.Should().Be(1);
        CheckResults();
    }

    [Fact]
    public void RemovedItemsAreNotResults()
    {
        var people = _personFaker.Generate(AddCount);
        var catPeople = _catPersonFaker.Generate(AddCount);

        _sourceCache.AddOrUpdate(people);
        _sourceCache.AddOrUpdate(catPeople);
        _sourceCache.Remove(_randomizer.ListItems(people, RemoveCount));
        _sourceCache.Remove(_randomizer.ListItems(catPeople, RemoveCount));

        _personResults.Summary.Overall.Adds.Should().Be(AddCount * 2);
        _personResults.Summary.Overall.Removes.Should().Be(RemoveCount * 2);
        _personResults.Messages.Count.Should().Be(4);
        _catPersonResults.Summary.Overall.Adds.Should().Be(AddCount);
        _catPersonResults.Summary.Overall.Removes.Should().Be(RemoveCount);
        _catPersonResults.Messages.Count.Should().Be(2);
        CheckResults();
    }

    [Fact]
    public void UpdateResultsAreCorrect()
    {
        // Arrange
        var people = _personFaker.Generate(AddCount);
        var catPeople = _catPersonFaker.Generate(AddCount);

        _sourceCache.AddOrUpdate(people);
        _sourceCache.AddOrUpdate(catPeople);

        var updates = _randomizer.ListItems(people.Concat(catPeople).ToList(), UpdateCount);
        var preUpdateCatPeople = updates.Where(p => p is CatPerson).ToList();
        var updated = updates.Select(p => GenerateUpdateRandom(p.Id)).ToList();
        var postUpdateCatPeople = updated.Where(p => p is CatPerson).ToList();
        var catToCatCount = preUpdateCatPeople.Count(p => postUpdateCatPeople.Any(pu => pu.Id == p.Id));
        var catToNonCount = preUpdateCatPeople.Count - catToCatCount;
        var nonToCatCount = postUpdateCatPeople.Count(p => !preUpdateCatPeople.Any(pu => pu.Id == p.Id));

        // Act
        _sourceCache.AddOrUpdate(updated);

        // Assert
        _personResults.Summary.Overall.Adds.Should().Be(AddCount * 2);
        _personResults.Summary.Overall.Updates.Should().Be(UpdateCount);
        _personResults.Messages.Count.Should().Be(3);
        _catPersonResults.Summary.Overall.Adds.Should().Be(AddCount + nonToCatCount);
        _catPersonResults.Summary.Overall.Removes.Should().Be(catToNonCount);
        _catPersonResults.Summary.Overall.Updates.Should().Be(catToCatCount);
        _catPersonResults.Messages.Count.Should().Be(2);
        CheckResults();
    }

    public void Dispose()
    {
        _sourceCache.Dispose();
        _personResults.Dispose();
        _catPersonResults.Dispose();
    }

    private IEnumerable<Person> GeneratePeople(int count = AddCount) => Enumerable.Range(0, count).Select(_ => _randomizer.Bool() ? _personFaker.Generate() : _catPersonFaker.Generate());

    private Person GenerateUpdateRandom(string id) => _randomizer.Bool() ? GenerateUpdatePerson(id) : GenerateUpdateCatPerson(id);

    private Person GenerateUpdatePerson(string id) => new(_personFaker.Generate().Name, id);

    private CatPerson GenerateUpdateCatPerson(string id)
    {
        var newCp = _catPersonFaker.Generate();
        return new CatPerson(newCp.Name, newCp.CatName, id);
    }

    private void CheckResults()
    {
        var expectedPeople = _sourceCache.Items;
        var expectedCatPeople = expectedPeople.OfType<CatPerson>();

        _personResults.Data.Items.Should().BeEquivalentTo(expectedPeople);
        _catPersonResults.Data.Items.Should().BeEquivalentTo(expectedCatPeople);
    }

    private interface ICatPerson
    {
        string CatName { get; }
    }

    private record Person(string Name, string Id)
    {
        public Person(string Name) : this(Name, Guid.NewGuid().ToString("N")) { }
    }

    private record CatPerson(string Name, string CatName, string Id) : Person(Name, Id), ICatPerson
    {
        public CatPerson(string Name, string CatName) : this(Name, CatName, Guid.NewGuid().ToString("N")) { }
    }
}
