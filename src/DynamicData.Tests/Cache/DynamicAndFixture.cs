using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class DynamicAndFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceList<IObservable<IChangeSet<Person, string>>> _source;

    private readonly ISourceCache<Person, string> _source1;

    private readonly ISourceCache<Person, string> _source2;

    private readonly ISourceCache<Person, string> _source3;

    public DynamicAndFixture()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _source3 = new SourceCache<Person, string>(p => p.Name);
        _source = new SourceList<IObservable<IChangeSet<Person, string>>>();
        _results = _source.And().AsAggregator();
    }

    [Test]
    public async Task AddAndRemoveLists()
    {
        var items = _generator.Take(100).ToArray();
        _source1.AddOrUpdate(items.Take(20));
        _source2.AddOrUpdate(items.Skip(10).Take(10));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(items.Skip(10).Take(10));

        _source.Add(_source3.Connect());
        await Assert.That(_results.Data.Count).IsEqualTo(0);

        _source.RemoveAt(2);
        await Assert.That(_results.Data.Count).IsEqualTo(10);
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _source3.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task RemoveAllLists()
    {
        var items = _generator.Take(100).ToArray();

        _source1.AddOrUpdate(items.Take(10));
        _source2.AddOrUpdate(items.Skip(20).Take(10));
        _source3.AddOrUpdate(items.Skip(30));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source.RemoveAt(2);
        _source.RemoveAt(1);
        _source.RemoveAt(0);
        //s _source.Clear();

        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemovingFromOneRemovesFromResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        _source2.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingBothProducesResults()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should have no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Cache should have no items");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task UpdatingOneProducesOnlyOneUpdate()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        var personUpdated = new Person("Adult1", 51);
        _source2.AddOrUpdate(personUpdated);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Cache should have no items");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(personUpdated).Because("Should be updated person");
    }

    [Test]
    public async Task UpdatingOneSourceOnlyProducesNoResults()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }
}
