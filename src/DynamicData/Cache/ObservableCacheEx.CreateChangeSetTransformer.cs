// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Executes the CreateChangeSetTransformer operation.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <returns>The result of the operation.</returns>
    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).AsObservableChangeSet(keySelector);

    /// <summary>
    /// Executes the CreateChangeSetTransformer operation.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <typeparam name="TCollection">The type of the TCollection value.</typeparam>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <returns>The result of the operation.</returns>
    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).ToObservableChangeSet<TCollection, TDestination>().AddKey(keySelector);

    /// <summary>
    /// Executes the CreateChangeSetTransformer operation.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="manySelector">The manySelector value.</param>
    /// <returns>The result of the operation.</returns>
    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).Connect();
}
