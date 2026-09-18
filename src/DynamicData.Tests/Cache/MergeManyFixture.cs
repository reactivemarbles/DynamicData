namespace DynamicData.Tests.Cache;

public class MergeManyFixture : IDisposable
{
    private readonly SourceCache<ObjectWithObservable, int> _source;

    public MergeManyFixture() => _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        stream.Dispose();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsFalse();
    }

    /// <summary>
    /// Invocations the only when child is invoked.
    /// </summary>
    [Test]
    public async Task InvocationOnlyWhenChildIsInvoked()
    {
        var invoked = false;

        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        await Assert.That(invoked).IsFalse();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsTrue();
        stream.Dispose();
    }

    [Test]
    public async Task RemovedItemWillNotCauseInvocation()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);
        _source.Remove(item);
        await Assert.That(invoked).IsFalse();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsFalse();
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
