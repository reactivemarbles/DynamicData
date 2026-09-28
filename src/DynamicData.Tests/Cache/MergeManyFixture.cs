using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class MergeManyFixture : IDisposable
{
    private readonly SourceCache<ObjectWithObservable, int> _source;

    public MergeManyFixture() => _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);

    public void Dispose() => _source.Dispose();

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

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
        var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);
        _source.Remove(item);
        invoked.Should().BeFalse();

        item.InvokeObservable(true);
        invoked.Should().BeFalse();
        stream.Dispose();
    }

    private class ObjectWithObservable(int id)
    {
        private readonly ISubject<bool> _changed = new Subject<bool>();

        private bool _value;

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed.AsObservable();

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

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var child = new Subject<int>();
        using var subscription = source.Connect().MergeMany(_ => child).Subscribe(_ => { }, ex => error = ex, () => { });

        source.AddOrUpdate(new Person("a", 1));
        child.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>("Merge propagates a failure from any inner stream");
    }
}
