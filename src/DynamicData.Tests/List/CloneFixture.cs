using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class CloneFixture : IDisposable
{
    private readonly IDisposable _cloner;

    private readonly ICollection<Person> _collection = new Collection<Person>();

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISourceCache<Person, string> _source;

    public CloneFixture()
    {
        _collection = new Collection<Person>();
        _source = new SourceCache<Person, string>(p => p.Name);
        _cloner = _source.Connect().Clone(_collection).Subscribe();
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BatchAdd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
    }

    [Test]
    public async Task BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _cloner.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
