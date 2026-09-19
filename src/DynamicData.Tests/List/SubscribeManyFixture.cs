namespace DynamicData.Tests.List;

public class SubscribeManyFixture : IDisposable
{
    private readonly ChangeSetAggregator<SubscribeableObject> _results;

    private readonly ISourceList<SubscribeableObject> _source;

    public SubscribeManyFixture()
    {
        _source = new SourceList<SubscribeableObject>();
        _results = new ChangeSetAggregator<SubscribeableObject>(
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
        _source.Add(new SubscribeableObject(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0].IsSubscribed).IsTrue().Because("Should be subscribed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    //[Test]
    //public void UpdateUnsubscribesPrevious()
    //{
    //	_source.Add(new SubscribeableObject(1));
    //	_source.AddOrUpdate(new SubscribeableObject(1)));

    //	Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
    //	Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
    //	Assert.AreEqual(true, _results.Messages[1].First().Current.IsSubscribed, "Current should be subscribed");
    //	Assert.AreEqual(false, _results.Messages[1].First().Previous.Value.IsSubscribed, "Previous should not be subscribed");
    //}

    [Test]
    public async Task EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        _source.AddRange(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i)));
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");

        var items = _results.Messages[0].SelectMany(x => x.Range);

        await Assert.That(items.All(d => !d.IsSubscribed)).IsTrue();
    }

    [Test]
    public async Task RemoveIsUnsubscribed()
    {
        _source.Add(new SubscribeableObject(1));
        _source.RemoveAt(0);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 0 items in the cache");
        await Assert.That(_results.Messages[1].First().Item.Current.IsSubscribed).IsFalse().Because("Should be be unsubscribed");
    }

    private class SubscribeableObject(int id)
    {
        public bool IsSubscribed { get; private set; }

        private int Id { get; } = id;

        public void Subscribe() => IsSubscribed = true;

        public void UnSubscribe() => IsSubscribed = false;
    }
}
