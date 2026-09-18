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

    [Test]
    public async Task InitialItemReturnsFalseWhenObservaleHasNoValue()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        await Assert.That(valueReturned.HasValue).IsTrue();
        await Assert.That(valueReturned!.Value).IsFalse().Because("The intial value should be false");

        subscribed.Dispose();
    }

    [Test]
    public async Task InlineObservableChangeProducesResult()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item = new ObjectWithObservable(1);
        item.InvokeObservable(true);
        _source.AddOrUpdate(item);

        await Assert.That(valueReturned.HasValue).IsTrue();
        await Assert.That(valueReturned!.Value).IsTrue().Because("Value should be true");
        subscribed.Dispose();
    }

    [Test]
    public async Task MultipleValuesReturnTrue()
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

        await Assert.That(valueReturned.Value).IsFalse().Because("Value should be false");

        item1.InvokeObservable(true);
        await Assert.That(valueReturned.Value).IsTrue().Because("Value should be true");
        subscribed.Dispose();
    }

    // https://github.com/reactivemarbles/DynamicData/issues/922
    [Test]
    public async Task ValuesPublishedOnSubscriptionDoNotTriggerPrematureOutput()
    {
        var item1 = new ObjectWithObservable(1);
        var item2 = new ObjectWithObservable(2);

        item2.InvokeObservable(true);

        _source.AddOrUpdate(item1);
        _source.AddOrUpdate(item2);

        using var subscription = _observable
            .ValidateSynchronization()
            .RecordValues(out var results);

        await Assert.That(results.RecordedValues.Count).IsEqualTo(1).Because("No items were added to the source, and no value changes were made to the items");
        await Assert.That(results.RecordedValues[0]).IsTrue().Because("One of the two items in the source has a true value");
    }

    private class ObjectWithObservable(int id) : IDisposable
    {
        private readonly ReactiveUI.Primitives.Signals.ISignal<bool> _changed = new ReactiveUI.Primitives.Signals.Signal<bool>();

        public int Id { get; } = id;

        public IObservable<bool> Observable => _changed;

        public bool Value { get; private set; }

        public void InvokeObservable(bool value)
        {
            Value = value;
            _changed.OnNext(value);
        }

        public void Dispose()
        {
            _changed.Dispose();
        }
    }
}
