using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class ForEachChangeFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _source;

    public ForEachChangeFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task Test()
    {
        var messages = new List<Change<Person, string>>();
        var messageWriter = _source.Connect().ForEachChange(messages.Add).Subscribe();

        _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
        messageWriter.Dispose();

        await Assert.That(messages.Count).IsEqualTo(100);
    }
}
