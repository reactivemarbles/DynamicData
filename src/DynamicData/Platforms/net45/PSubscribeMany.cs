// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive.PLinq
#else
namespace DynamicData.PLinq
#endif
{
/// <summary>
/// Provides members for the PSubscribeMany class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="subscriptionFactory">The subscriptionFactory value.</param>
/// <param name="parallelisationOptions">The parallelisationOptions value.</param>
internal sealed class PSubscribeMany<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
        where TObject : notnull
        where TKey : notnull
    {
        /// <summary>
        /// The _source field.
        /// </summary>
        private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// The _subscriptionFactory field.
        /// </summary>
        private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));

        /// <summary>
        /// Executes the Run operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var published = _source.Publish();
                        var subscriptions = published.Transform((t, k) => _subscriptionFactory(t, k), parallelisationOptions).DisposeMany().Subscribe();

                        return new CompositeDisposable(subscriptions, published.SubscribeSafe(observer), published.Connect());
                    });
    }
}

#endif
