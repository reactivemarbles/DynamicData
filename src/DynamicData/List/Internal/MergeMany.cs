// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

internal sealed class MergeMany<T, TDestination>(IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
    where T : notnull
{
    private readonly Func<T, IObservable<TDestination>> _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));

    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<TDestination> Run() => Observable.Create<TDestination>(
            observer =>
            {
                var counter = new SubscriptionCounter();
                var locker = InternalEx.NewLock();
                var disposable = _source.Concat(counter.DeferCleanup)
                                                .SubscribeMany(t => SubscribeChild(t, locker, counter, observer))
                                                .Subscribe(_ => { }, observer.OnError, observer.OnCompleted);

                return new CompositeDisposable(disposable, counter);
            });

#if NET9_0_OR_GREATER
    private IDisposable SubscribeChild(T item, Lock locker, SubscriptionCounter counter, IObserver<TDestination> observer)
#else
    private IDisposable SubscribeChild(T item, object locker, SubscriptionCounter counter, IObserver<TDestination> observer)
#endif
    {
        counter.Added();
        try
        {
            return _observableSelector(item).Synchronize(locker).Finally(counter.Finally).Subscribe(observer.OnNext, _ => { }, () => { });
        }
        catch (ObjectDisposedException)
        {
            counter.Finally();
            return Disposable.Empty;
        }
    }

    private sealed class SubscriptionCounter : IDisposable
    {
        private readonly Signal<IChangeSet<T>> _subject = new();
        private int _subscriptionCount = 1;

        public IObservable<IChangeSet<T>> DeferCleanup => Observable.Defer(() =>
        {
            CheckCompleted();
            return _subject.AsObservable();
        });

        public void Added() => _ = Interlocked.Increment(ref _subscriptionCount);

        public void Finally() => CheckCompleted();

        public void Dispose() => _subject.Dispose();

        private void CheckCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCount) == 0)
            {
                _subject.OnCompleted();
            }
        }
    }
}
