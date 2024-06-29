using System;
using System.Linq;
using System.Reactive.Disposables;

using FluentAssertions;

using Xunit;

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

    [Fact]
    public void AddedItemWillbeSubscribed()
    {
        _source.Add(new SubscribeableObject(1));

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].IsSubscribed.Should().Be(true, "Should be subscribed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    //[Fact]
    //public void UpdateUnsubscribesPrevious()
    //{
    //	_source.Add(new SubscribeableObject(1));
    //	_source.AddOrUpdate(new SubscribeableObject(1)));

    //	Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
    //	Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
    //	Assert.AreEqual(true, _results.Messages[1].First().Current.IsSubscribed, "Current should be subscribed");
    //	Assert.AreEqual(false, _results.Messages[1].First().Previous.Value.IsSubscribed, "Previous should not be subscribed");
    //}

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        _source.AddRange(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i)));
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");

        var items = _results.Messages[0].SelectMany(x => x.Range);

        items.All(d => !d.IsSubscribed).Should().BeTrue();
    }

    [Fact]
    public void RemoveIsUnsubscribed()
    {
        _source.Add(new SubscribeableObject(1));
        _source.RemoveAt(0);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
        _results.Messages[1].First().Item.Current.IsSubscribed.Should().Be(false, "Should be be unsubscribed");
    }

    private class SubscribeableObject(int id)
    {
        public bool IsSubscribed { get; private set; }

        private int Id { get; } = id;

        public void Subscribe() => IsSubscribed = true;

        public void UnSubscribe() => IsSubscribed = false;
    }
}
