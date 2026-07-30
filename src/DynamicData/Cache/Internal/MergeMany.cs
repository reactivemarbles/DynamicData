// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the MergeMany class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
internal sealed class MergeMany<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _observableSelector field.
    /// </summary>
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="MergeMany{TObject, TKey, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="observableSelector">The observableSelector value.</param>
    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MergeMany{TObject, TKey, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="observableSelector">The observableSelector value.</param>
    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<TDestination> Run() => Observable.Create<TDestination>(
            observer =>
            {
                var counter = new StrongBox<int>(1);
                var queue = new DeliveryQueue<TDestination>(observer);

                // Queue first: terminate before subscription disposal to prevent
                // Finally callbacks from delivering spurious OnCompleted during teardown.
                return new CompositeDisposable(queue, _source
                    .Do(static _ => { }, static _ => { }, () => CheckCompleted(counter, queue))
                    .Concat(Observable.Never<IChangeSet<TObject, TKey>>())
                    .SubscribeMany((t, key) =>
                    {
                        Interlocked.Increment(ref counter.Value);
                        return _observableSelector(t, key)
                            .Finally(() => CheckCompleted(counter, queue))
                            .Subscribe(queue.OnNext, static _ => { });
                    })
                    .Subscribe(static _ => { }, observer.OnError));
            });

    /// <summary>
    /// Executes the CheckCompleted operation.
    /// </summary>
    /// <param name="counter">The counter value.</param>
    /// <param name="queue">The queue value.</param>
    private static void CheckCompleted(StrongBox<int> counter, DeliveryQueue<TDestination> queue)
    {
        if (Interlocked.Decrement(ref counter.Value) == 0 && !queue.IsTerminated)
        {
            queue.OnCompleted();
        }
    }
}
