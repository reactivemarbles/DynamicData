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
    /// Adds or updates the cache with the specified item, producing a changeset with a single <b>Add</b>
    /// (if the key is new) or <b>Update</b> (if the key already exists).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item mutation inside <c>ISourceCache&lt;TObject,TKey&gt;.Edit</c>.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Produced when the key does not already exist in the cache.</description></item>
    /// <item><term>Update</term><description>Produced when the key already exists. The previous value is included in the changeset.</description></item>
    /// <item><term>Remove</term><description>Not produced by this method.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this method.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>EditDiff&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, IEnumerable&lt;TObject&gt;, IEqualityComparer&lt;TObject&gt;)</c></seealso>
    /// <seealso><c>Remove&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, TObject)</c></seealso>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(item);

        source.Edit(updater => updater.AddOrUpdate(item));
    }

    /// <summary>
    /// Provides an overload of <c>AddOrUpdate</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="equalityComparer">The <c>IEqualityComparer&lt;TObject&gt;</c> used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>This overload uses <paramref name="equalityComparer"/> to suppress no-op updates when the new value equals the existing one.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(item);
        ArgumentExceptionHelper.ThrowIfNull(equalityComparer);

        source.Edit(updater => updater.AddOrUpdate(item, equalityComparer));
    }

    /// <summary>
    /// Provides an overload of <c>AddOrUpdate</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to add or update items in.</param>
    /// <param name="items">The <c>IEnumerable&lt;TObject&gt;</c> of items to add or update.</param>
    /// <remarks>Batch overload. All items are added/updated inside a single <c>ISourceCache&lt;TObject,TKey&gt;.Edit</c> call, producing one changeset.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(items);

        source.Edit(updater => updater.AddOrUpdate(items));
    }

    /// <summary>
    /// Provides an overload of <c>AddOrUpdate</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to add or update items in.</param>
    /// <param name="items">The <c>IEnumerable&lt;TObject&gt;</c> of items to add or update.</param>
    /// <param name="equalityComparer">The <c>IEqualityComparer&lt;TObject&gt;</c> used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>Batch overload with equality comparison. All items are added/updated inside a single <c>ISourceCache&lt;TObject,TKey&gt;.Edit</c> call.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(items);
        ArgumentExceptionHelper.ThrowIfNull(equalityComparer);

        source.Edit(updater => updater.AddOrUpdate(items, equalityComparer));
    }

    /// <summary>
    /// Provides an overload of <c>AddOrUpdate</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>IIntermediateCache&lt;TObject, TKey&gt;</c> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to associate with the item.</param>
    /// <remarks>This overload operates on <c>IIntermediateCache&lt;TObject, TKey&gt;</c>, which requires an explicit key parameter.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(item);
        ArgumentExceptionHelper.ThrowIfNull(key);

        source.Edit(updater => updater.AddOrUpdate(item, key));
    }
}
