using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class MergeManyWithKeyOverloadFixture : IDisposable
{
    private readonly ISourceCache<ObjectWithObservable, int> _source;

    public MergeManyWithKeyOverloadFixture() => _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);

    public void Dispose() => _source.Dispose();

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany((o, key) => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        stream.Dispose();

        item.InvokeObservable(true);
        invoked.Should().BeFalse();
    }

    [Fact]
    public void InvocationOnlyWhenChildIsInvoked()
    {
        var invoked = false;

        var stream = _source.Connect().MergeMany((o, key) => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        invoked.Should().BeFalse();

        item.InvokeObservable(true);
        invoked.Should().BeTrue();
        stream.Dispose();
    }

    [Fact]
    public void RemovedItemWillNotCauseInvocation()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany((o, key) => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);
        _source.Remove(item);
        invoked.Should().BeFalse();

        item.InvokeObservable(true);
        invoked.Should().BeFalse();
        stream.Dispose();
    }

    [Fact]
    public void SingleItemCompleteWillNotMergedStream()
    {
        var completed = false;
        var stream = _source.Connect().MergeMany((o, key) => o.Observable).Subscribe(_ => { }, () => completed = true);

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        item.InvokeObservable(true);
        item.CompleteObservable();

        stream.Dispose();

        completed.Should().BeFalse();
    }

    [Fact]
    public void SingleItemFailWillNotFailMergedStream()
    {
        var failed = false;
        var stream = _source.Connect().MergeMany((o, key) => o.Observable).Subscribe(_ => { }, ex => failed = true);

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        item.FailObservable(new Exception("Test exception"));

        stream.Dispose();

        failed.Should().BeFalse();
    }

    /// <summary>
    /// Merged stream does not complete if a child stream is still active.
    /// </summary>
    [Fact]
    public void MergedStreamDoesNotCompleteWhileItemStreamActive()
    {
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany((o, i) => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

        _source.Dispose();

        sourceCompleted.Should().BeTrue();
        streamCompleted.Should().BeFalse();
    }

    /// <summary>
    /// Stream completes only when source and all child are complete.
    /// </summary>
    [Fact]
    public void MergedStreamCompletesWhenSourceAndItemsComplete()
    {
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany((o, i) => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

        _source.Dispose();
        item.CompleteObservable();

        sourceCompleted.Should().BeTrue();
        streamCompleted.Should().BeTrue();
    }

    /// <summary>
    /// Stream completes even if one of the children fails.
    /// </summary>
    [Fact]
    public void MergedStreamCompletesIfLastItemFails()
    {
        var receivedError = default(Exception);
        var streamCompleted = false;
        var sourceCompleted = false;

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany((o, i) => o.Observable).Subscribe(_ => { }, err => receivedError = err, () => streamCompleted = true);

        _source.Dispose();
        item.FailObservable(new Exception("Test exception"));

        receivedError.Should().Be(default);
        sourceCompleted.Should().BeTrue();
        streamCompleted.Should().BeTrue();
    }

    /// <summary>
    /// If the source stream has an error, the merged steam should also.
    /// </summary>
    [Fact]
    public void MergedStreamFailsWhenSourceFails()
    {
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<ObjectWithObservable, int>>(expectedError);
        var stream = _source.Connect().Concat(throwObservable)
                .MergeMany((o, i) => o.Observable).Subscribe(_ => { }, err => receivedError = err);

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        _source.Dispose();

        receivedError.Should().Be(expectedError);
    }

    private class ObjectWithObservable(int id)
    {
        private readonly ISubject<bool> _changed = new Subject<bool>();

        private bool _value;

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed.AsObservable();

        public void CompleteObservable() => _changed.OnCompleted();

        public void FailObservable(Exception ex) => _changed.OnError(ex);

        public void InvokeObservable(bool value)
        {
            _value = value;
            _changed.OnNext(value);
        }
    }
}
