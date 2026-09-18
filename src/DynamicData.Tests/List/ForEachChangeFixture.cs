#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class ForEachChangeFixture : IDisposable
{
    private readonly ISourceList<Person> _source;

    public ForEachChangeFixture() => _source = new SourceList<Person>();

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task EachChangeInokesTheCallback()
    {
        var messages = new List<Change<Person>>();

        var messageWriter = _source.Connect().ForEachChange(messages.Add).Subscribe();

        var people = new RandomPersonGenerator().Take(100);
        people.ForEach(_source.Add);

        await Assert.That(messages.Count).IsEqualTo(100);
        messageWriter.Dispose();
    }

    [Test]
    public async Task EachItemChangeInokesTheCallbac2()
    {
        var messages = new List<ItemChange<Person>>();

        var messageWriter = _source.Connect().ForEachItemChange(messages.Add).Subscribe();
        _source.AddRange(new RandomPersonGenerator().Take(5));
        _source.InsertRange(new RandomPersonGenerator().Take(5), 2);
        _source.AddRange(new RandomPersonGenerator().Take(5));

        await Assert.That(messages.Count).IsEqualTo(15);
        messageWriter.Dispose();
    }

    [Test]
    public async Task EachItemChangeInokesTheCallback()
    {
        var messages = new List<ItemChange<Person>>();

        var messageWriter = _source.Connect().ForEachItemChange(messages.Add).Subscribe();

        _source.AddRange(new RandomPersonGenerator().Take(100));

        await Assert.That(messages.Count).IsEqualTo(100);
        messageWriter.Dispose();
    }
}
