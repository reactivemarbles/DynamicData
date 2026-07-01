// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Signals downstream operators to re-evaluate the specified item. Produces a changeset with a single <b>Refresh</b> change.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to signal re-evaluation on.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to refresh.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a Refresh inside <c>ISourceCache&lt;TObject,TKey&gt;.Edit</c>. A Refresh does not change data in the cache; it signals downstream operators (such as <c>Filter&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, bool&gt;, bool)</c> or <c>Sort&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IComparer&lt;TObject&gt;, SortOptimisations, int)</c>) to re-evaluate the item.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>AutoRefresh&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>SuppressRefresh&lt;TObject, TKey&gt;</c></seealso>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Refresh(item));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate the specified items. Produces one changeset with a <b>Refresh</b> for each item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to signal re-evaluation on.</param>
    /// <param name="items">The <c>IEnumerable&lt;TObject&gt;</c> of items to refresh.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Refresh(items));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate all items in the cache. Produces one changeset with a <b>Refresh</b> for every item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to signal re-evaluation on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Refresh());
    }
}
