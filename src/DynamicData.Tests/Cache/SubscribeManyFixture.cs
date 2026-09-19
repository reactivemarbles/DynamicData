namespace DynamicData.Tests.Cache;

public class SubscribeManyFixture : IDisposable
{
    private readonly ChangeSetAggregator<SubscribeableObject, int> _results;

    private readonly ISourceCache<SubscribeableObject, int> _source;

    public SubscribeManyFixture()
    {
        _source = new SourceCache<SubscribeableObject, int>(p => p.Id);
        _results = new ChangeSetAggregator<SubscribeableObject, int>(
            _source.Connect().SubscribeMany(
                subscribeable =>
                {
                    subscribeable.Subscribe();
                    return Disposable.Create(subscribeable.UnSubscribe);
                }));
    }

    [Test]
    public async Task AddedItemWillbeSubscribed()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0].IsSubscribed).IsTrue().Because("Should be subscribed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i)));
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[1].All(d => !d.Current.IsSubscribed)).IsTrue();
    }

    [Test]
    public async Task RemoveIsUnsubscribed()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));
        _source.Remove(1);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 0 items in the cache");
        await Assert.That(_results.Messages[1].First().Current.IsSubscribed).IsFalse().Because("Should be be unsubscribed");
    }

    [Test]
    public async Task UpdateUnsubscribesPrevious()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));
        _source.AddOrUpdate(new SubscribeableObject(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 items in the cache");
        await Assert.That(_results.Messages[1].First().Current.IsSubscribed).IsTrue().Because("Current should be subscribed");
        await Assert.That(_results.Messages[1].First().Previous.Value.IsSubscribed).IsFalse().Because("Previous should not be subscribed");
    }

    private class SubscribeableObject(int id)
    {
        public int Id { get; } = id;

        public bool IsSubscribed { get; private set; }

        public void Subscribe() => IsSubscribed = true;

        public void UnSubscribe() => IsSubscribed = false;
    }
}
