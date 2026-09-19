using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

[InheritsTests]
public class ExceptFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable() => _targetSource.Connect().Except(_exceptSource.Connect());
}

[InheritsTests]
public class ExceptCollectionFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<Person, string>>> { _targetSource.Connect(), _exceptSource.Connect() };
        return l.Except();
    }
}

public abstract class ExceptFixtureBase : IDisposable
{
    protected ISourceCache<Person, string> _exceptSource;

    protected ISourceCache<Person, string> _targetSource;

    private readonly ChangeSetAggregator<Person, string> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected ExceptFixtureBase()
    {
        _targetSource = new SourceCache<Person, string>(p => p.Name);
        _exceptSource = new SourceCache<Person, string>(p => p.Name);
        _results = CreateObservable().AsAggregator();
    }

    public void Dispose()
    {
        _targetSource.Dispose();
        _exceptSource.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task DoNotIncludeExceptListItems()
    {
        var person = new Person("Adult1", 50);
        _exceptSource.AddOrUpdate(person);
        _targetSource.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task RemovedAnItemFromExceptThenIncludesTheItem()
    {
        var person = new Person("Adult1", 50);
        _exceptSource.AddOrUpdate(person);
        _targetSource.AddOrUpdate(person);

        _exceptSource.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Cache should have no items");
    }

    [Test]
    public async Task UpdatingOneSourceOnlyProducesResult()
    {
        var person = new Person("Adult1", 50);
        _targetSource.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
    }

    protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();
}
