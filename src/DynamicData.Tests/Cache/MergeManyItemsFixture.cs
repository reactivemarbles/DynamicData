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

public class MergeManyItemsFixture : IDisposable
{
    private readonly ISourceCache<ObjectWithObservable, int> _source;

    public MergeManyItemsFixture() => _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);

    public void Dispose() => _source.Dispose();

    [Fact]
    public void EverythingIsUnsubscribedWhenStreamIsDisposed()
    {
        var invoked = false;
        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                (o.Item.Id == 1).Should().BeTrue();
            });

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

        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                (o.Item.Id == 1).Should().BeTrue();
            });

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
        var stream = _source.Connect().MergeManyItems(o => o.Observable).Subscribe(
            o =>
            {
                invoked = true;
                (o.Item.Id == 1).Should().BeTrue();
            });

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
    public void CompletesWhenTheSourceCompletes()
    {
        var completed = false;

        using var source = new Subject<IChangeSet<Person, string>>();
        using var subscription = source.MergeManyItems(_ => Observable.Empty<int>()).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void StaysOpenWhenOnlyAChildCompletes()
    {
        var completed = false;

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var subscription = source.Connect().MergeManyItems(_ => Observable.Return(1)).Subscribe(_ => { }, () => completed = true);

        source.AddOrUpdate(new Person("a", 1));

        completed.Should().BeFalse("one child finishing does not finish the merge");
    }

    [Fact]
    public void DeliversAnErrorRaisedByAChild()
    {
        Exception? error = null;

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var child = new Subject<int>();
        using var subscription = source.Connect().MergeManyItems(_ => child).Subscribe(_ => { }, ex => error = ex, () => { });

        source.AddOrUpdate(new Person("a", 1));
        child.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }
}
