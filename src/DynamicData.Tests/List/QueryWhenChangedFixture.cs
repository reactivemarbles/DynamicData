using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class QueryWhenChangedFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public QueryWhenChangedFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<Person>(_source.Connect(p => p.Age > 20));
    }

    [Test]
    public async Task CanHandleAddsAndUpdates()
    {
        var invoked = false;
        var subscription = _source.Connect().QueryWhenChanged(q => q.Count).Subscribe(query => invoked = true);

        var person = new Person("A", 1);
        _source.Add(person);
        _source.Remove(person);

        await Assert.That(invoked).IsTrue();
        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnNext()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);

        await Assert.That(invoked).IsFalse();

        _source.Add(new Person("A", 1));
        await Assert.That(invoked).IsTrue();

        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnNext_WithSelector()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);

        await Assert.That(invoked).IsFalse();

        _source.Add(new Person("A", 1));
        await Assert.That(invoked).IsTrue();

        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnSubscriptionIfItHasData()
    {
        var invoked = false;
        _source.Add(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);
        await Assert.That(invoked).IsTrue();
        subscription.Dispose();
    }

    [Test]
    public async Task ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
    {
        var invoked = false;
        _source.Add(new Person("A", 1));
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
