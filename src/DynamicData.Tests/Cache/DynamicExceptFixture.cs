using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class DynamicExceptFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceList<IObservable<IChangeSet<Person, string>>> _source;

    private readonly ISourceCache<Person, string> _source1;

    private readonly ISourceCache<Person, string> _source2;

    private readonly ISourceCache<Person, string> _source3;

    public DynamicExceptFixture()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _source3 = new SourceCache<Person, string>(p => p.Name);
        _source = new SourceList<IObservable<IChangeSet<Person, string>>>();
        _results = _source.Except().AsAggregator();
    }

    [Test]
    public async Task AddAndRemoveLists()
    {
        var items = _generator.Take(100).OrderBy(p => p.Name).ToArray();

        _source1.AddOrUpdate(items);
        _source2.AddOrUpdate(items.Take(10));
        _source3.AddOrUpdate(items.Skip(90).Take(10));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        await Assert.That(_results.Data.Count).IsEqualTo(80);
        await Assert.That(_results.Data.Items).IsEquivalentTo(items.Skip(10).Take(80));

        _source.RemoveAt(2);
        await Assert.That(_results.Data.Count).IsEqualTo(90);
        await Assert.That(_results.Data.Items).IsEquivalentTo(items.Skip(10));

        _source.RemoveAt(0);
        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(items.Take(10));
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
    public async Task DoNotIncludeExceptListItems()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source2.AddOrUpdate(person);
        _source1.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task RemoveAllLists()
    {
        var items = _generator.Take(100).ToArray();

        _source1.AddOrUpdate(items.Take(10));
        _source2.AddOrUpdate(items.Skip(10).Take(10));
        _source3.AddOrUpdate(items.Skip(20));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source.Clear();

        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemovedAnItemFromExceptThenIncludesTheItem()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source2.AddOrUpdate(person);
        _source1.AddOrUpdate(person);

        _source2.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingOneSourceOnlyProducesResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
    }
}
