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

    [Test]
    public async Task AddWillNotCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0].IsDisposed).IsFalse().Because("Should not be disposed");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task EverythingIsDisposedWhenStreamIsDisposed()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new DisposableObject(i)));
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[1].All(d => d.Current.IsDisposed)).IsTrue();
    }

    [Test]
    public async Task RemoveWillCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));
        _source.Edit(updater => updater.Remove(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 0 items in the cache");
        await Assert.That(_results.Messages[1].First().Current.IsDisposed).IsTrue().Because("Should be disposed");
    }

    [Test]
    public async Task UpdateWillCallDispose()
    {
        _source.AddOrUpdate(new DisposableObject(1));
        _source.AddOrUpdate(new DisposableObject(1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 items in the cache");
        await Assert.That(_results.Messages[1].First().Current.IsDisposed).IsFalse().Because("Current should not be disposed");
        await Assert.That(_results.Messages[1].First().Previous.Value.IsDisposed).IsTrue().Because("Previous should be disposed");
    }

    private class DisposableObject(int id) : IDisposable
    {
        public int Id { get; private set; } = id;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
