namespace DynamicData.Tests.Cache;

public sealed class StaleChildErrorRegressionFixture
{
    [Test]
    public async Task StaleChildOnErrorDuringSameKeyReplacementDoesNotTerminateCurrentSubscription()
    {
        var staleError = new InvalidOperationException("stale child disposal error");
        using var source = new SourceCache<Item, int>(static item => item.Id);
        var oldItem = new Item(1, "old");
        var newItem = new Item(1, "new");
        var oldChild = new ErrorOnDisposeObservable<string>(oldItem.Name, staleError);
        var newChild = new ErrorOnDisposeObservable<string>(newItem.Name, staleError);
        Exception? observedError = null;
        var messages = new List<IChangeSet<string, int>>();

        source.AddOrUpdate(oldItem);

        using var subscription = source.Connect()
            .TransformOnObservable((item, _) => ReferenceEquals(item, oldItem) ? oldChild : newChild)
            .Subscribe(
                onNext: messages.Add,
                onError: error => observedError = error);

        source.AddOrUpdate(newItem);

        await Assert.That(oldChild.DisposeCount).IsEqualTo(1);
        await Assert.That(observedError).IsNull();
        await Assert.That(messages.SelectMany(static changes => changes).Last().Current).IsEqualTo(newItem.Name);
    }

    [Test]
    public async Task StaleChildOnErrorAfterSameKeyReplacementDoesNotTerminateCurrentSubscription()
    {
        var staleError = new InvalidOperationException("stale child late error");
        using var source = new SourceCache<Item, int>(static item => item.Id);
        var oldItem = new Item(1, "old");
        var newItem = new Item(1, "new");
        var oldChild = new ManualErrorObservable<string>(oldItem.Name);
        var newChild = new ManualErrorObservable<string>(newItem.Name);
        Exception? observedError = null;
        var messages = new List<IChangeSet<string, int>>();

        source.AddOrUpdate(oldItem);

        using var subscription = source.Connect()
            .TransformOnObservable((item, _) => ReferenceEquals(item, oldItem) ? oldChild : newChild)
            .Subscribe(
                onNext: messages.Add,
                onError: error => observedError = error);

        source.AddOrUpdate(newItem);
        oldChild.OnError(staleError);

        await Assert.That(oldChild.DisposeCount).IsEqualTo(1);
        await Assert.That(observedError).IsNull();
        await Assert.That(messages.SelectMany(static changes => changes).Last().Current).IsEqualTo(newItem.Name);
    }

    private sealed record Item(int Id, string Name);

    private sealed class ErrorOnDisposeObservable<T>(T value, Exception disposeError) : IObservable<T>
    {
        private readonly Exception _disposeError = disposeError;
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            return new Subscription(this, observer);
        }

        private sealed class Subscription(ErrorOnDisposeObservable<T> owner, IObserver<T> observer) : IDisposable
        {
            public void Dispose()
            {
                if (Interlocked.Increment(ref owner._disposeCount) == 1)
                {
                    observer.OnError(owner._disposeError);
                }
            }
        }
    }

    private sealed class ManualErrorObservable<T>(T value) : IObservable<T>
    {
        private IObserver<T>? _observer;
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            observer.OnNext(value);
            return new Subscription(this);
        }

        public void OnError(Exception error)
            => _observer?.OnError(error);

        private sealed class Subscription(ManualErrorObservable<T> owner) : IDisposable
        {
            public void Dispose()
                => Interlocked.Increment(ref owner._disposeCount);
        }
    }
}
