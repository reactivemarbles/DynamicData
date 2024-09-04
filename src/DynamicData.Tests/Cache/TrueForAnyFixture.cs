using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TrueForAnyFixture : IDisposable
{
    private readonly IObservable<bool> _observable;

    private readonly ISourceCache<ObjectWithObservable, int> _source;

    public TrueForAnyFixture()
    {
        _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
        _observable = _source.Connect().TrueForAny(o => o.Observable.StartWith(o.Value), o => o == true);
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InitialItemReturnsFalseWhenObservaleHasNoValue()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        valueReturned.HasValue.Should().BeTrue();
        valueReturned!.Value.Should().Be(false, "The intial value should be false");

        subscribed.Dispose();
    }

    [Fact]
    public void InlineObservableChangeProducesResult()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item = new ObjectWithObservable(1);
        item.InvokeObservable(true);
        _source.AddOrUpdate(item);

        valueReturned.HasValue.Should().BeTrue();
        valueReturned!.Value.Should().Be(true, "Value should be true");
        subscribed.Dispose();
    }

    [Fact]
    public void MultipleValuesReturnTrue()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item1 = new ObjectWithObservable(1);
        var item2 = new ObjectWithObservable(2);
        var item3 = new ObjectWithObservable(3);
        _source.AddOrUpdate(item1);
        _source.AddOrUpdate(item2);
        _source.AddOrUpdate(item3);

        if (valueReturned is null)
        {
            throw new InvalidOperationException(nameof(valueReturned));
        }

        valueReturned.Value.Should().Be(false, "Value should be false");

        item1.InvokeObservable(true);
        valueReturned.Value.Should().Be(true, "Value should be true");
        subscribed.Dispose();
    }

    // https://github.com/reactivemarbles/DynamicData/issues/922
    [Fact]
    public void ValuesPublishedOnSubscriptionDoNotTriggerPrematureOutput()
    {
        var item1 = new ObjectWithObservable(1);
        var item2 = new ObjectWithObservable(2);

        item2.InvokeObservable(true);

        _source.AddOrUpdate(item1);
        _source.AddOrUpdate(item2);

        using var subscription = _observable
            .ValidateSynchronization()
            .RecordValues(out var results);

        results.RecordedValues.Count.Should().Be(1, because: "No items were added to the source, and no value changes were made to the items");
        results.RecordedValues[0].Should().Be(true, because: "One of the two items in the source has a true value");
    }

    private class ObjectWithObservable(int id)
    {
        private readonly ISubject<bool> _changed = new Subject<bool>();

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed;

        public bool Value { get; private set; }

        public void InvokeObservable(bool value)
        {
            Value = value;
            _changed.OnNext(value);
        }
    }
}
