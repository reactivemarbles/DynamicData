namespace DynamicData.Tests.Cache;

public class TrueForAllFixture : IDisposable
{
    private readonly IObservable<bool> _observable;

    private readonly ISourceCache<ObjectWithObservable, int> _source;

    public TrueForAllFixture()
    {
        _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
        _observable = _source.Connect().TrueForAll(o => o.Observable.StartWith(o.Value), (obj, invoked) => invoked);
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task InitialItemReturnsFalseWhenObservableHasNoValue()
    {
        bool? valueReturned = null;
        var subscribed = _observable.Subscribe(result => { valueReturned = result; });

        var item = new ObjectWithObservable(1);
        _source.AddOrUpdate(item);

        await Assert.That(valueReturned.HasValue).IsTrue();

        if (valueReturned is null)
        {
            throw new InvalidOperationException(nameof(valueReturned));
        }

        await Assert.That(valueReturned.Value).IsFalse().Because("The initial value should be false");

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

        if (valueReturned is null)
        {
            throw new InvalidOperationException(nameof(valueReturned));
        }

        await Assert.That(valueReturned.Value).IsTrue().Because("Value should be true");
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
        item2.InvokeObservable(true);
        item3.InvokeObservable(true);
        await Assert.That(valueReturned.Value).IsTrue().Because("Value should be true");

        subscribed.Dispose();
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
