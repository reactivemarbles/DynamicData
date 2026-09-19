#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class IObservableListBindCacheFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly IObservableList<Person> _list;

    private readonly ChangeSetAggregator<Person> _listNotifications;

    private readonly ISourceCache<Person, string> _source;

    private readonly ChangeSetAggregator<Person, string> _sourceCacheNotifications;

    public IObservableListBindCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _sourceCacheNotifications = _source.Connect().AutoRefresh().BindToObservableList(out _list).AsAggregator();

        _listNotifications = _list.Connect().AsAggregator();
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_list.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_list.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BatchAdd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_list.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_list.Items).IsEquivalentTo(people).Because("Collections should be equivalent");
    }

    [Test]
    public async Task BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _sourceCacheNotifications.Dispose();
        _listNotifications.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task ListRecievesRefresh()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        person.Age = 60;

        await Assert.That(_listNotifications.Messages.Count).IsEqualTo(2);
        await Assert.That(_listNotifications.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        await Assert.That(_list.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_list.Items[0]).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
