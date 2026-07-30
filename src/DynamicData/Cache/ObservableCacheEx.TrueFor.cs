// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
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
    /// Executes the TrueFor operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TValue">The type of the TValue value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="observableSelector">The observableSelector value.</param>
    /// <param name="collectionMatcher">The collectionMatcher value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => new TrueFor<TObject, TKey, TValue>(source, observableSelector, collectionMatcher).Run();
}
