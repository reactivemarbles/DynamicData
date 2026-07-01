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
    /// Removes the specified item from the cache. Produces a <b>Remove</b> changeset if the item exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to remove.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item removal inside <c>ISourceCache&lt;TObject,TKey&gt;.Edit</c>. The key is extracted from the item using the cache's key selector.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>AddOrUpdate&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, TObject)</c></seealso>
    /// <seealso><c>Clear&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;)</c></seealso>
    /// <seealso><c>RemoveKeys&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, IEnumerable&lt;TKey&gt;)</c></seealso>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(item));
    }

    /// <summary>
    /// Removes the item with the specified key from the cache. Produces a <b>Remove</b> changeset if the key exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// Removes the specified items from the cache. Any items not present in the cache are ignored.
    /// Produces a <b>Remove</b> changeset for each item that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="items">The <c>IEnumerable&lt;TObject&gt;</c> of items to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(items));
    }

    /// <summary>
    /// Removes the items with the specified keys from the cache. Any keys not present are ignored.
    /// Produces a <b>Remove</b> changeset for each key that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="keys">The <c>IEnumerable&lt;TKey&gt;</c> keys to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(keys));
    }

    /// <summary>
    /// Provides an overload of <c>Remove</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>IIntermediateCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <remarks>Overload that targets an <c>IIntermediateCache&lt;TObject, TKey&gt;</c>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// Provides an overload of <c>Remove</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>IIntermediateCache&lt;TObject, TKey&gt;</c> from which to remove items.</param>
    /// <param name="keys">The <c>IEnumerable&lt;TKey&gt;</c> keys to remove.</param>
    /// <remarks>Overload that targets an <c>IIntermediateCache&lt;TObject, TKey&gt;</c>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        source.Edit(updater => updater.Remove(keys));
    }
}
