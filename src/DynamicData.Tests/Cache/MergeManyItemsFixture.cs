using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

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
}
