using System;
using System.Linq;
using System.Reactive.Disposables;

using FluentAssertions;

using Xunit;

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

    [Fact]
    public void AddedItemWillbeSubscribed()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].IsSubscribed.Should().Be(true, "Should be subscribed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i)));
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[1].All(d => !d.Current.IsSubscribed).Should().BeTrue();
    }

    [Fact]
    public void RemoveIsUnsubscribed()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));
        _source.Remove(1);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
        _results.Messages[1].First().Current.IsSubscribed.Should().Be(false, "Should be be unsubscribed");
    }

    [Fact]
    public void UpdateUnsubscribesPrevious()
    {
        _source.AddOrUpdate(new SubscribeableObject(1));
        _source.AddOrUpdate(new SubscribeableObject(1));

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
        _results.Messages[1].First().Current.IsSubscribed.Should().Be(true, "Current should be subscribed");
        _results.Messages[1].First().Previous.Value.IsSubscribed.Should().Be(false, "Previous should not be subscribed");
    }

    private class SubscribeableObject(int id)
    {
        public int Id { get; } = id;

        public bool IsSubscribed { get; private set; }

        public void Subscribe() => IsSubscribed = true;

        public void UnSubscribe() => IsSubscribed = false;
    }
}
