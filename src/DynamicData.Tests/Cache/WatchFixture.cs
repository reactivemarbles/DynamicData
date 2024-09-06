using System;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class WatchFixture : IDisposable
{
    private readonly ChangeSetAggregator<DisposableObject, int> _results;

    private readonly ISourceCache<DisposableObject, int> _source;

    public WatchFixture()
    {
        _source = new SourceCache<DisposableObject, int>(p => p.Id);
        _results = new ChangeSetAggregator<DisposableObject, int>(_source.Connect().DisposeMany());
    }

    [Fact]
    public void AddWillNotCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].IsDisposed.Should().Be(false, "Should not be disposed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void EverythingIsDisposedWhenStreamIsDisposed()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new DisposableObject(i)));
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[1].All(d => d.Current.IsDisposed).Should().BeTrue();
    }

    [Fact]
    public void RemoveWillCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));
        _source.Edit(updater => updater.Remove(1));

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
        _results.Messages[1].First().Current.IsDisposed.Should().Be(true, "Should be disposed");
    }

    [Fact]
    public void UpdateWillCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));
        _source.AddOrUpdate(new DisposableObject(1));

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
        _results.Messages[1].First().Current.IsDisposed.Should().Be(false, "Current should not be disposed");
        _results.Messages[1].First().Previous.Value.IsDisposed.Should().Be(true, "Previous should be disposed");
    }

    private class DisposableObject(int id) : IDisposable
    {
        public int Id { get; private set; } = id;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
