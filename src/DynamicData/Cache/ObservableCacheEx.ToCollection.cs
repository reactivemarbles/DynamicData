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
    /// Converts the change set into a fully formed collection. Each change in the source results in a new collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to materialize into a collection on each change.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    /// <seealso><c>ObservableListEx.ToCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;)</c></seealso>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return Observable.Defer(() =>
        {
            Cache<TObject, TKey>? cache = null;

            return source.Select(changes =>
            {
                cache ??= new Cache<TObject, TKey>(changes.Count);
                cache.Clone(changes);

                // Snapshot directly, without allocating a query wrapper for every change set.
                return (IReadOnlyCollection<TObject>)new ReadOnlyCollectionLight<TObject>(cache.Items);
            });
        });
    }
}
