#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

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

    [Test]
    public async Task AddRange()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task Clear()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);
        _source.Clear();
        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.Add(person);
        _source.Replace(person, personUpdated);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
