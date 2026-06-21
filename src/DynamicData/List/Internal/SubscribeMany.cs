// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the SubscribeMany class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="subscriptionFactory">The subscriptionFactory value.</param>
internal sealed class SubscribeMany<T>(IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
    where T : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _subscriptionFactory field.
    /// </summary>
    private readonly Func<T, IDisposable> _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var shared = _source.Publish();
                var subscriptions = shared
                    .Transform(t => _subscriptionFactory(t))
                    .DisposeMany()
                    .SubscribeSafe(Observer.Create<IChangeSet<IDisposable>>(
                        onNext: static _ => { },
                        onError: observer.OnError,
                        onCompleted: static () => { }));

                return new CompositeDisposable(subscriptions, shared.SubscribeSafe(observer), shared.Connect());
            });
}
