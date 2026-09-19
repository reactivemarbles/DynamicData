namespace DynamicData.Tests.Cache;

public class MergeManyItemsFixture : IDisposable
{
    private readonly ISourceCache<ObjectWithObservable, int> _source;

    public MergeManyItemsFixture() => _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var observedIds = new List<int>();
        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                observedIds.Add(o.Item.Id);
            });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        stream.Dispose();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsFalse();
        await Assert.That(observedIds).IsEmpty();
    }

    [Test]
    public async Task InvocationOnlyWhenChildIsInvoked()
    {
        var invoked = false;
        var observedIds = new List<int>();

        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                observedIds.Add(o.Item.Id);
            });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        await Assert.That(invoked).IsFalse();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsTrue();
        await Assert.That(observedIds).IsEquivalentTo(new[] { 1 });
        stream.Dispose();
    }

    [Test]
    public async Task RemovedItemWillNotCauseInvocation()
    {
        var invoked = false;
        var observedIds = new List<int>();
        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                observedIds.Add(o.Item.Id);
            });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);
        _source.Remove(item);
        await Assert.That(invoked).IsFalse();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsFalse();
        await Assert.That(observedIds).IsEmpty();
        stream.Dispose();
    }

    private class ObjectWithObservable(int id) : IDisposable
    {
        private readonly ReactiveUI.Primitives.Signals.Signal<bool> _changed = new ReactiveUI.Primitives.Signals.Signal<bool>();

        private bool _value;

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed.AsObservable();

        public void InvokeObservable(bool value)
        {
            _value = value;
            _changed.OnNext(value);
        }
        public void Dispose()
        {
            _changed.Dispose();
        }
    }
}
