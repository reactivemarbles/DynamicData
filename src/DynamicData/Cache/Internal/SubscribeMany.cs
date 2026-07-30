// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the SubscribeMany class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class SubscribeMany<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    /// <summary>
    /// The _subscriptionFactory field.
    /// </summary>
    private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscribeMany{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="subscriptionFactory">The subscriptionFactory value.</param>
    public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
    {
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _subscriptionFactory = (t, _) => subscriptionFactory(t);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscribeMany{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="subscriptionFactory">The subscriptionFactory value.</param>
    public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var published = _source.Publish();
                var subscriptions = published
                    .Transform((t, k) => _subscriptionFactory(t, k))
                    .DisposeMany()
                    .SubscribeSafe(Observer.Create<IChangeSet<IDisposable, TKey>>(
                        onNext: static _ => { },
                        onError: observer.OnError,
                        onCompleted: static () => { }));

                return new CompositeDisposable(subscriptions, published.SubscribeSafe(observer), published.Connect());
            });
}
