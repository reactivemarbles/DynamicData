using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class IncludeUpdateFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public IncludeUpdateFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = new ChangeSetAggregator<Person, string>(_source.Connect().IncludeUpdateWhen((current, previous) => current != previous));
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task IgnoreFunctionWillIgnoreSubsequentUpdatesOfAnItem()
    {
        var person = new Person("Person", 10);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
    }
}
