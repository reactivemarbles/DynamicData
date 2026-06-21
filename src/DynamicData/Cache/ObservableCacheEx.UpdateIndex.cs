// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// Sets the <c>Index</c> property on each item (which must implement <see cref="IIndexAware"/>)
    /// to reflect its position in the sorted output. Operates on <c>ISortedChangeSet&lt;TObject, TKey&gt;</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;ISortedChangeSet&lt;TObject, TKey&gt;&gt;</c> to update index positions in.</param>
    /// <returns>An observable that emits the sorted changesets after updating item indices.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : IIndexAware
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));
    }
}
