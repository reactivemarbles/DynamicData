using Bogus;
using DynamicData.Tests.Domain;

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

    public OfTypeFixture()
    {
        _randomizer = new(0x3737_ddcc);
        _personFaker = new Faker<Person>().CustomInstantiator(faker => new Person(faker.Person.FullName)).WithSeed(_randomizer);
        _catPersonFaker = new Faker<CatPerson>().CustomInstantiator(faker => new CatPerson(faker.Person.FullName, $"{faker.Hacker.Adjective()} the {faker.Hacker.Noun()}")).WithSeed(_randomizer);
        var testOutput = TestContext.Current?.OutputWriter;
        _personResults = _sourceCache.Connect().TestSpy(testOutput, "Cache").AsAggregator();
        _catPersonResults = _sourceCache.Connect().OfType<Person, string, CatPerson>().TestSpy(testOutput, "OfType").AsAggregator();
    }

    [Test]
    public async Task AddedItemsAreInResults()
    {
        // Arrange
        var people = _personFaker.Generate(AddCount);
        var catPeople = _catPersonFaker.Generate(AddCount);

        _sourceCache.AddOrUpdate(people);
        _sourceCache.AddOrUpdate(catPeople);

        await Assert.That(_personResults.Summary.Overall.Adds).IsEqualTo(AddCount * 2);
        await Assert.That(_personResults.Messages.Count).IsEqualTo(2);
        await Assert.That(_catPersonResults.Summary.Overall.Adds).IsEqualTo(AddCount);
        await Assert.That(_catPersonResults.Messages.Count).IsEqualTo(1);
        await CheckResults();
    }

    [Test]
    public async Task RemovedItemsAreNotResults()
    {
        var people = _personFaker.Generate(AddCount);
        var catPeople = _catPersonFaker.Generate(AddCount);

        _sourceCache.AddOrUpdate(people);
        _sourceCache.AddOrUpdate(catPeople);
        _sourceCache.Remove(_randomizer.ListItems(people, RemoveCount));
        _sourceCache.Remove(_randomizer.ListItems(catPeople, RemoveCount));

        await Assert.That(_personResults.Summary.Overall.Adds).IsEqualTo(AddCount * 2);
        await Assert.That(_personResults.Summary.Overall.Removes).IsEqualTo(RemoveCount * 2);
        await Assert.That(_personResults.Messages.Count).IsEqualTo(4);
        await Assert.That(_catPersonResults.Summary.Overall.Adds).IsEqualTo(AddCount);
        await Assert.That(_catPersonResults.Summary.Overall.Removes).IsEqualTo(RemoveCount);
        await Assert.That(_catPersonResults.Messages.Count).IsEqualTo(2);
        await CheckResults();
    }

    [Test]
    public async Task UpdateResultsAreCorrect()
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
        await Assert.That(_personResults.Summary.Overall.Adds).IsEqualTo(AddCount * 2);
        await Assert.That(_personResults.Summary.Overall.Updates).IsEqualTo(UpdateCount);
        await Assert.That(_personResults.Messages.Count).IsEqualTo(3);
        await Assert.That(_catPersonResults.Summary.Overall.Adds).IsEqualTo(AddCount + nonToCatCount);
        await Assert.That(_catPersonResults.Summary.Overall.Removes).IsEqualTo(catToNonCount);
        await Assert.That(_catPersonResults.Summary.Overall.Updates).IsEqualTo(catToCatCount);
        await Assert.That(_catPersonResults.Messages.Count).IsEqualTo(2);
        await CheckResults();
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

    private async Task CheckResults()
    {
        var expectedPeople = _sourceCache.Items;
        var expectedCatPeople = expectedPeople.OfType<CatPerson>();

        await Assert.That(_personResults.Data.Items).IsEquivalentTo(expectedPeople);
        await Assert.That(_catPersonResults.Data.Items).IsEquivalentTo(expectedCatPeople);
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
