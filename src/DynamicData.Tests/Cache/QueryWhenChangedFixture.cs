using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class QueryWhenChangedFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public QueryWhenChangedFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = new ChangeSetAggregator<Person, string>(_source.Connect(p => p.Age > 20));
    }

    [Test]
    public async Task ChangeInvokedOnNext()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);

        await Assert.That(invoked).IsFalse();

        _source.AddOrUpdate(new Person("A", 1));
        await Assert.That(invoked).IsTrue();

        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnNext_WithSelector()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);

        await Assert.That(invoked).IsFalse();

        _source.AddOrUpdate(new Person("A", 1));
        await Assert.That(invoked).IsTrue();

        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnSubscriptionIfItHasData()
    {
        var invoked = false;
        _source.AddOrUpdate(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);
        await Assert.That(invoked).IsTrue();
        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
    {
        var invoked = false;
        _source.AddOrUpdate(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);
        await Assert.That(invoked).IsTrue();
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
