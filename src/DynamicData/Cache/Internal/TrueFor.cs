// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TrueFor class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TValue">The type of the TValue value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="observableSelector">The observableSelector value.</param>
/// <param name="collectionMatcher">The collectionMatcher value.</param>
internal sealed class TrueFor<TObject, TKey, TValue>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
    where TObject : notnull
    where TKey : notnull
    where TValue : notnull
{
    /// <summary>
    /// The _collectionMatcher field.
    /// </summary>
    private readonly Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> _collectionMatcher = collectionMatcher ?? throw new ArgumentNullException(nameof(collectionMatcher));

    /// <summary>
    /// The _observableSelector field.
    /// </summary>
    private readonly Func<TObject, IObservable<TValue>> _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<bool> Run()
        => Observable.Create<bool>(observer =>
        {
            var itemsWithValues = _source
                .Transform(item => new ObservableWithValue<TObject, TValue>(
                    item: item,
                    source: _observableSelector.Invoke(item)))
                .Publish();

            var subscription = itemsWithValues.MergeMany(item => item.Observable).CombineLatest(
                    itemsWithValues.ToCollection(),
                    // We don't need to actually look at the changed values, we just need them as a trigger to re-evaluate the matcher method.
                    (_, itemsWithValues) => _collectionMatcher.Invoke(itemsWithValues))
                .DistinctUntilChanged()
                .SubscribeSafe(observer);

            return new CompositeDisposable(
                subscription,
                itemsWithValues.Connect());
        });
}
