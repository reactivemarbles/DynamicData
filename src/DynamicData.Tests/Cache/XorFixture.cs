using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

[InheritsTests]
public class XOrFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable() => _source1.Connect().Xor(_source2.Connect());
}

[InheritsTests]
public class XOrCollectionFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<Person, string>>> { _source1.Connect(), _source2.Connect() };
        return l.Xor();
    }
}

public abstract class XOrFixtureBase : IDisposable
{
    protected ISourceCache<Person, string> _source1;

    protected ISourceCache<Person, string> _source2;

    private readonly ChangeSetAggregator<Person, string> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected XOrFixtureBase()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _results = CreateObservable().AsAggregator();
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task RemovingFromOneDoesNotFromResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        _source2.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingBothDoeNotProducesResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingOneProducesOnlyOneUpdate()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        var personUpdated = new Person("Adult1", 51);
        _source2.AddOrUpdate(personUpdated);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingOneSourceOnlyProducesResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
    }

    protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();
}
