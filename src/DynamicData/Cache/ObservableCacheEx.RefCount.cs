// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Cache-aware equivalent of <c>Publish().RefCount()</c>. An internal cache is created on the first subscriber
    /// and disposed when the last subscriber unsubscribes. All subscribers share the same upstream subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to share via reference counting.</param>
    /// <returns>A ref-counted observable changeset stream.</returns>
    /// <seealso cref="AsObservableCache{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IObservable<IChangeSet<TObject, TKey>> RefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<TObject, TKey>(source).Run();
    }
}
