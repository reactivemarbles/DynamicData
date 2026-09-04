using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class MergeManyFixture : IDisposable
{
    private readonly ISourceList<ObjectWithObservable> _source;

    public MergeManyFixture() => _source = new SourceList<ObjectWithObservable>();

    public void Dispose() => _source.Dispose();

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        stream.Dispose();

        item.InvokeObservable(true);
        invoked.Should().BeFalse();
    }

    /// <summary>
    /// Invocations the only when child is invoked.
    /// </summary>
    [Fact]
    public void InvocationOnlyWhenChildIsInvoked()
    {
        var invoked = false;

        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        invoked.Should().BeFalse();

        item.InvokeObservable(true);
        invoked.Should().BeTrue();
        stream.Dispose();
    }

    [Fact]
    public void RemovedItemWillNotCauseInvocation()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.Add(item);
        _source.Remove(item);
        invoked.Should().BeFalse();

        item.InvokeObservable(true);
        invoked.Should().BeFalse();
        stream.Dispose();
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
        _source.Add(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

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
        _source.Add(item);

        using var stream = _source.Connect().Do(_ => { }, () => sourceCompleted = true)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, () => streamCompleted = true);

        _source.Dispose();
        item.CompleteObservable();

        sourceCompleted.Should().BeTrue();
        streamCompleted.Should().BeTrue();
    }

    /// <summary>
    /// Stream completes even if one of the children fails.
    /// </summary>
    [Fact]
    public void MergedStreamFailsIfLastItemFails()
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

        sourceCompleted.Should().BeTrue();
        receivedError.Should().NotBeNull("Merge propagates a failure from any inner stream");
        streamCompleted.Should().BeFalse("a failure and a completion are mutually exclusive");
    }

    /// <summary>
    /// If the source stream has an error, the merged steam should also.
    /// </summary>
    [Fact]
    public void MergedStreamFailsWhenSourceFails()
    {
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<ObjectWithObservable>>(expectedError);
        var stream = _source.Connect().Concat(throwObservable)
                .MergeMany(o => o.Observable).Subscribe(_ => { }, err => receivedError = err);

        var item = new ObjectWithObservable(1);
        _source.Add(item);

        _source.Dispose();

        receivedError.Should().Be(expectedError);
    }

    private class ObjectWithObservable(int id)
    {
        private readonly ISubject<bool> _changed = new Subject<bool>();

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
    }

    [Fact]
    public void DeliversAnErrorRaisedByAChild()
    {
        Exception? error = null;

        using var source = new SourceList<Person>();
        using var child = new Subject<int>();
        using var subscription = source.Connect().MergeMany(_ => child).Subscribe(_ => { }, ex => error = ex, () => { });

        source.Add(new Person("a", 1));
        child.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>("Merge propagates a failure from any inner stream");
    }
}
