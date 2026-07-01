// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the MergeMany class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="observableSelector">The observableSelector value.</param>
internal sealed class MergeMany<T, TDestination>(IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
    where T : notnull
{
    /// <summary>
    /// The _observableSelector field.
    /// </summary>
    private readonly Func<T, IObservable<TDestination>> _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the SubscribeChild operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="locker">The locker value.</param>
    /// <param name="counter">The counter value.</param>
    /// <param name="observer">The observer value.</param>
    /// <returns>The result of the operation.</returns>
    private IDisposable SubscribeChild(T item, Lock locker, SubscriptionCounter counter, IObserver<TDestination> observer)
#else

    /// <summary>
    /// Executes the SubscribeChild operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="locker">The locker value.</param>
    /// <param name="counter">The counter value.</param>
    /// <param name="observer">The observer value.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Provides members for the SubscriptionCounter class.
/// </summary>
private sealed class SubscriptionCounter : IDisposable
    {
        /// <summary>
        /// The _subject field.
        /// </summary>
        private readonly Signal<IChangeSet<T>> _subject = new();

        /// <summary>
        /// The _subscriptionCount field.
        /// </summary>
        private int _subscriptionCount = 1;

        /// <summary>
        /// Gets the DeferCleanup value.
        /// </summary>
        public IObservable<IChangeSet<T>> DeferCleanup => Observable.Defer(() =>
        {
            CheckCompleted();
            return _subject.AsObservable();
        });

        /// <summary>
        /// Executes the Added operation.
        /// </summary>
        public void Added() => _ = Interlocked.Increment(ref _subscriptionCount);

        /// <summary>
        /// Executes the Finally operation.
        /// </summary>
        public void Finally() => CheckCompleted();

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        public void Dispose() => _subject.Dispose();

        /// <summary>
        /// Executes the CheckCompleted operation.
        /// </summary>
        private void CheckCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCount) == 0)
            {
                _subject.OnCompleted();
            }
        }
    }
}
