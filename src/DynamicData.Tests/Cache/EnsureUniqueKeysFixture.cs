using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class EnsureUniqueKeysFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _source;
    private readonly ChangeSetAggregator<Person, string> _results;

    public EnsureUniqueKeysFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect(suppressEmptyChangeSets: false).EnsureUniqueKeys().AsAggregator();
    }

    [Test]
    public async Task UniqueForAdds()
    {
        _source.Edit(innerCache =>
        {
            innerCache.AddOrUpdate(new Person("Me", 20));
            innerCache.AddOrUpdate(new Person("Me", 21));
            innerCache.AddOrUpdate(new Person("Me", 22));
        });

        var message1 = _results.Messages[0];
        await Assert.That(message1.Count).IsEqualTo(1);
        await Assert.That(message1.First().Current.Age).IsEqualTo(22);
        await Assert.That(message1.First().Reason).IsEqualTo(ChangeReason.Add);
    }

    [Test]
    public async Task AddAndRemove()
    {
        _source.Edit(innerCache =>
        {
            innerCache.AddOrUpdate(new Person("Me", 20));
            innerCache.AddOrUpdate(new Person("Me", 21));
            innerCache.RemoveKey("Me");
        });

        var message1 = _results.Messages[0];
        await Assert.That(message1.Count).IsEqualTo(0);

    }

    [Test]
    public async Task Refresh()
    {
        _source.AddOrUpdate(new Person("Me", 20));

        _source.Edit(innerCache =>
        {
            innerCache.Refresh("Me");
        });

        var message1 = _results.Messages[1];
        await Assert.That(message1.Count).IsEqualTo(1);
        await Assert.That(message1.First().Current.Age).IsEqualTo(20);
        await Assert.That(message1.First().Reason).IsEqualTo(ChangeReason.Refresh);

    }

    [Test]
    public async Task CompoundRefresh1()
    {
        _source.Edit(innerCache =>
        {
            _source.AddOrUpdate(new Person("Me", 20));
            innerCache.Refresh("Me");
        });

        var message1 = _results.Messages[0];
        await Assert.That(message1.Count).IsEqualTo(1);
        await Assert.That(message1.First().Current.Age).IsEqualTo(20);
        await Assert.That(message1.First().Reason).IsEqualTo(ChangeReason.Add);

    }

    [Test]
    public async Task CompoundRefresh2()
    {
        _source.Edit(innerCache =>
        {
            innerCache.AddOrUpdate(new Person("Me", 20));
            innerCache.AddOrUpdate(new Person("Me", 21));
            innerCache.Refresh("Me");
            innerCache.Refresh("Me");
        });

        var message1 = _results.Messages[0];
        await Assert.That(message1.Count).IsEqualTo(1);
        await Assert.That(message1.First().Current.Age).IsEqualTo(21);
        await Assert.That(message1.First().Reason).IsEqualTo(ChangeReason.Add);

    }

    [Test]
    public async Task CompoundRefresh3()
    {
        _source.AddOrUpdate(new Person("Me", 20));

        _source.Edit(innerCache =>
        {

            innerCache.Refresh("Me");
            innerCache.Refresh("Me");
            innerCache.Refresh("Me");
        });

        var message1 = _results.Messages[1];
        await Assert.That(message1.Count).IsEqualTo(1);
        await Assert.That(message1.First().Current.Age).IsEqualTo(20);
        await Assert.That(message1.First().Reason).IsEqualTo(ChangeReason.Refresh);

    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
