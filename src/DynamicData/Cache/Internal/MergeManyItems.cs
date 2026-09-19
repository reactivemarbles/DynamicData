// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the MergeManyItems class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
internal sealed class MergeManyItems<TObject, TKey, TDestination>
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
    /// Initializes a new instance of the <see cref="MergeManyItems{TObject, TKey, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="observableSelector">The observableSelector value.</param>
    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        _source = source;
        _observableSelector = observableSelector;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MergeManyItems{TObject, TKey, TDestination}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="observableSelector">The observableSelector value.</param>
    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        _source = source;
        _observableSelector = (t, _) => observableSelector(t);
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<ItemWithValue<TObject, TDestination>> Run() => Observable.Create<ItemWithValue<TObject, TDestination>>(observer => _source.SubscribeMany((t, v) => _observableSelector(t, v).Select(z => new ItemWithValue<TObject, TDestination>(t, z)).SubscribeSafe(observer)).Subscribe());
}
