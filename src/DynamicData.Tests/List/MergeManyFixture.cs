namespace DynamicData.Tests.List;

public class MergeManyFixture : IDisposable
{
    private readonly ISourceList<ObjectWithObservable> _source;

    public MergeManyFixture() => _source = new SourceList<ObjectWithObservable>();

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.Add(item);

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
        _source.Add(item);

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
        _source.Add(item);
        _source.Remove(item);
        await Assert.That(invoked).IsFalse();

        item.InvokeObservable(true);
        await Assert.That(invoked).IsFalse();
        stream.Dispose();
    }

    /// <summary>
    /// Merged stream does not complete if a child stream is still active.
    /// </summary>
    [Test]
    public async Task MergedStreamDoesNotCompleteWhileItemStreamActive()
    {
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

        _source.Dispose();

        await Assert.That(sourceCompleted).IsTrue();
        await Assert.That(streamCompleted).IsFalse();
    }

    /// <summary>
    /// Stream completes only when source and all child are complete.
    /// </summary>
    [Test]
    public async Task MergedStreamCompletesWhenSourceAndItemsComplete()
    {
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

        _source.Dispose();
        item.CompleteObservable();

        await Assert.That(sourceCompleted).IsTrue();
        await Assert.That(streamCompleted).IsTrue();
    }

    /// <summary>
    /// Stream completes even if one of the children fails.
    /// </summary>
    [Test]
    public async Task MergedStreamCompletesIfLastItemFails()
    {
        var receivedError = default(Exception);
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, err => receivedError = err, () => streamCompleted = true);

        _source.Dispose();
        item.FailObservable(new Exception("Test exception"));

        await Assert.That(receivedError).IsNull();
        await Assert.That(sourceCompleted).IsTrue();
        await Assert.That(streamCompleted).IsTrue();
    }

    /// <summary>
    /// If the source stream has an error, the merged steam should also.
    /// </summary>
    [Test]
    public async Task MergedStreamFailsWhenSourceFails()
    {
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<ObjectWithObservable>>(expectedError);
        var stream = _source.Connect().Concat(throwObservable)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, err => receivedError = err);

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        _source.Dispose();

        await Assert.That(receivedError).IsEqualTo(expectedError);
    }

    private class ObjectWithObservable(int id) : IDisposable
    {
        private readonly ReactiveUI.Primitives.Signals.ISignal<bool> _changed = new ReactiveUI.Primitives.Signals.Signal<bool>();

        private bool _value;

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed;

        public void CompleteObservable() => _changed.OnCompleted();

        public void FailObservable(Exception ex) => _changed.OnError(ex);

        public void InvokeObservable(bool value)
        {
            _value = value;
            _changed.OnNext(value);
        }

        public void Dispose() => _changed.Dispose();
    }
}
