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
    /// Provides an overload of <c>EditDiff</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to diff and update.</param>
    /// <param name="allItems">The <c>IEnumerable&lt;TObject&gt;</c> representing the complete desired state to diff against the cache.</param>
    /// <param name="equalityComparer">An <c>IEqualityComparer&lt;TObject&gt;</c> used to determine whether a new item is the same as an existing cached item.</param>
    /// <remarks>
    /// This overload uses an <c>IEqualityComparer&lt;T&gt;</c> instead of a <c>Func&lt;T, T, TResult&gt;</c> delegate
    /// to determine item equality.
    /// </remarks>
    public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(allItems);
        ArgumentExceptionHelper.ThrowIfNull(equalityComparer);

        source.EditDiff(allItems, equalityComparer.Equals);
    }

    /// <summary>
    /// Diffs a complete snapshot of items against the current cache contents, producing the minimal set of
    /// Add, Update, and Remove changes needed to bring the cache in sync with the snapshot.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to diff and update.</param>
    /// <param name="allItems">The <c>IEnumerable&lt;TObject&gt;</c> representing the complete desired state.</param>
    /// <param name="areItemsEqual">The <c>Func&lt;TObject, TObject, bool&gt;</c> that returns <see langword="true"/> when the current and previous items are considered equal, e.g. <c>(current, previous) =&gt; current.Version == previous.Version</c>.</param>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Items in <paramref name="allItems"/> whose key is not in the cache produce an <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Items present in both <paramref name="allItems"/> and the cache that differ (per <paramref name="areItemsEqual"/>) produce an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Items in the cache whose key is not in <paramref name="allItems"/> produce a <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operation.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="allItems"/>, or <paramref name="areItemsEqual"/> is <see langword="null"/>.</exception>
    public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, Func<TObject, TObject, bool> areItemsEqual)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(allItems);
        ArgumentExceptionHelper.ThrowIfNull(areItemsEqual);

        var editDiff = new EditDiff<TObject, TKey>(source, areItemsEqual);
        editDiff.Edit(allItems);
    }

    /// <summary>
    /// Converts an <c>IObservable&lt;T&gt;</c> of <c>IEnumerable&lt;T&gt;</c> into a changeset stream by diffing each
    /// emission against the previous one. Each emission replaces the entire dataset.
    /// Counterpart to <c>ToCollection&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;TObject&gt;&gt;</c> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">The <c>Func&lt;TObject, TKey&gt;</c> that extracts the unique key from each item.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> for comparing items. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable changeset representing the incremental differences between successive snapshots.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Items in the new snapshot whose key was not in the previous snapshot produce an <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Items present in both snapshots that differ (per <paramref name="equalityComparer"/>) produce an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Items in the previous snapshot whose key is absent from the new snapshot produce a <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operator.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso><c>ToCollection&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return new EditDiffChangeSet<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an <c>IObservable&lt;T&gt;</c> of <c>Optional&lt;T&gt;</c> into a changeset stream that tracks
    /// a single item: <c>Some</c> produces an <b>Add</b> or <b>Update</b>, and <c>None</c> produces a <b>Remove</b>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;Optional&lt;TObject&gt;&gt;</c> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">The <c>Func&lt;TObject, TKey&gt;</c> that extracts the unique key from each item.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> for comparing items. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable changeset tracking the single optional item.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emitted when the source produces <c>Some(value)</c> and no item was previously tracked.</description></item>
    /// <item><term>Update</term><description>Emitted when the source produces <c>Some(value)</c> and an item was already tracked with a different value (per <paramref name="equalityComparer"/>).</description></item>
    /// <item><term>Remove</term><description>Emitted when the source produces <c>None</c> and an item was previously tracked.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operator.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<ReactiveUI.Primitives.Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return new EditDiffChangeSetOptional<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }
}
