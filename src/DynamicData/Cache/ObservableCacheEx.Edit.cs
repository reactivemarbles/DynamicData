// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for source cache editing helpers.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Adds or updates the cache with the specified item, producing a changeset with a single <b>Add</b>
    /// (if the key is new) or <b>Update</b> (if the key already exists).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item mutation inside <see cref="ISourceCache{TObject,TKey}.Edit"/>.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Produced when the key does not already exist in the cache.</description></item>
    /// <item><term>Update</term><description>Produced when the key already exists. The previous value is included in the changeset.</description></item>
    /// <item><term>Remove</term><description>Not produced by this method.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this method.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="EditDiff{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject}, IEqualityComparer{TObject})"/>
    /// <seealso cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(item));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="equalityComparer">The <see cref="IEqualityComparer{TObject}"/> used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>This overload uses <paramref name="equalityComparer"/> to suppress no-op updates when the new value equals the existing one.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(item, equalityComparer));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to add or update items in.</param>
    /// <param name="items">The <see cref="IEnumerable{TObject}"/> of items to add or update.</param>
    /// <remarks>Batch overload. All items are added/updated inside a single <see cref="ISourceCache{TObject,TKey}.Edit"/> call, producing one changeset.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to add or update items in.</param>
    /// <param name="items">The <see cref="IEnumerable{TObject}"/> of items to add or update.</param>
    /// <param name="equalityComparer">The <see cref="IEqualityComparer{TObject}"/> used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>Batch overload with equality comparison. All items are added/updated inside a single <see cref="ISourceCache{TObject,TKey}.Edit"/> call.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items, equalityComparer));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source">The <see cref="IIntermediateCache{TObject, TKey}"/> to add or update items in.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to associate with the item.</param>
    /// <remarks>This overload operates on <see cref="IIntermediateCache{TObject, TKey}"/>, which requires an explicit key parameter.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        item.ThrowArgumentNullExceptionIfNull(nameof(item));

        source.Edit(updater => updater.AddOrUpdate(item, key));
    }

    /// <summary>
    /// Removes all items from the cache, producing a changeset with a <b>Remove</b> for every item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to clear.</param>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Not produced by this operation.</description></item>
    /// <item><term>Update</term><description>Not produced by this operation.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted for every item currently in the cache.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operation.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <inheritdoc cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <inheritdoc cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    public static void Clear<TObject, TKey>(this LockFreeObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        source.Edit(updater => updater.Clear());
    }

    /// <summary>
    /// Applies each change from the source changeset to the specified <paramref name="target"/> collection as a side effect.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to clone.</param>
    /// <param name="target">The <see cref="ICollection{TObject}"/> target collection to which changes are applied.</param>
    /// <returns>An observable that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The item is added to <paramref name="target"/>. Forwarded as <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>The previous item is removed from <paramref name="target"/> and the current item is added. Forwarded as <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>The item is removed from <paramref name="target"/>. Forwarded as <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Ignored (<see cref="ICollection{T}"/> has no concept of refresh). Forwarded as <b>Refresh</b>.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ICollection<TObject> target)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        target.ThrowArgumentNullExceptionIfNull(nameof(target));

        return source.Do(
            changes =>
            {
                foreach (var item in changes.ToConcreteType())
                {
                    switch (item.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                target.Add(item.Current);
                            }

                            break;

                        case ChangeReason.Update:
                            {
                                target.Remove(item.Previous.Value);
                                target.Add(item.Current);
                            }

                            break;

                        case ChangeReason.Remove:
                            target.Remove(item.Current);
                            break;
                    }
                }
            });
    }

    /// <inheritdoc cref="EditDiff{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject}, Func{TObject, TObject, bool})"/>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to diff and update.</param>
    /// <param name="allItems">The <see cref="IEnumerable{TObject}"/> representing the complete desired state to diff against the cache.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> used to determine whether a new item is the same as an existing cached item.</param>
    /// <remarks>
    /// This overload uses an <see cref="IEqualityComparer{T}"/> instead of a <see cref="Func{T, T, TResult}"/> delegate
    /// to determine item equality.
    /// </remarks>
    public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        allItems.ThrowArgumentNullExceptionIfNull(nameof(allItems));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        source.EditDiff(allItems, equalityComparer.Equals);
    }

    /// <summary>
    /// Diffs a complete snapshot of items against the current cache contents, producing the minimal set of
    /// Add, Update, and Remove changes needed to bring the cache in sync with the snapshot.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to diff and update.</param>
    /// <param name="allItems">The <see cref="IEnumerable{TObject}"/> representing the complete desired state.</param>
    /// <param name="areItemsEqual">The <see cref="Func{TObject, TObject, bool}"/> that returns <see langword="true"/> when the current and previous items are considered equal, e.g. <c>(current, previous) =&gt; current.Version == previous.Version</c>.</param>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        allItems.ThrowArgumentNullExceptionIfNull(nameof(allItems));
        areItemsEqual.ThrowArgumentNullExceptionIfNull(nameof(areItemsEqual));

        var editDiff = new EditDiff<TObject, TKey>(source, areItemsEqual);
        editDiff.Edit(allItems);
    }

    /// <summary>
    /// Converts an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> into a changeset stream by diffing each
    /// emission against the previous one. Each emission replaces the entire dataset.
    /// Counterpart to <see cref="ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{TObject}}"/> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">The <see cref="Func{TObject, TKey}"/> that extracts the unique key from each item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> for comparing items. Uses default equality if <see langword="null"/>.</param>
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
    /// <seealso cref="ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSet<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an <see cref="IObservable{T}"/> of <see cref="Optional{T}"/> into a changeset stream that tracks
    /// a single item: <c>Some</c> produces an <b>Add</b> or <b>Update</b>, and <c>None</c> produces a <b>Remove</b>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{Optional{TObject}}"/> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">The <see cref="Func{TObject, TKey}"/> that extracts the unique key from each item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> for comparing items. Uses default equality if <see langword="null"/>.</param>
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
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSetOptional<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Calls <c>Evaluate()</c> on items that implement <see cref="IEvaluateAware"/> when a <b>Refresh</b> change arrives.
    /// Other change reasons are forwarded without invoking Evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to trigger re-evaluation on.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Calls <c>Evaluate()</c> on the item, then forwards the change.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : IEvaluateAware
        where TKey : notnull => source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Refresh).ForEach(u => u.Current.Evaluate()));

    /// <summary>
    /// Signals downstream operators to re-evaluate the specified item. Produces a changeset with a single <b>Refresh</b> change.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to signal re-evaluation on.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to refresh.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a Refresh inside <see cref="ISourceCache{TObject,TKey}.Edit"/>. A Refresh does not change data in the cache; it signals downstream operators (such as <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> or <see cref="Sort{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, SortOptimisations, int)"/>) to re-evaluate the item.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="SuppressRefresh{TObject, TKey}"/>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(item));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate the specified items. Produces one changeset with a <b>Refresh</b> for each item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to signal re-evaluation on.</param>
    /// <param name="items">The <see cref="IEnumerable{TObject}"/> of items to refresh.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(items));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate all items in the cache. Produces one changeset with a <b>Refresh</b> for every item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to signal re-evaluation on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh());
    }

    /// <summary>
    /// Removes the specified item from the cache. Produces a <b>Remove</b> changeset if the item exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to remove.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item removal inside <see cref="ISourceCache{TObject,TKey}.Edit"/>. The key is extracted from the item using the cache's key selector.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <seealso cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    /// <seealso cref="RemoveKeys{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TKey})"/>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(item));
    }

    /// <summary>
    /// Removes the item with the specified key from the cache. Produces a <b>Remove</b> changeset if the key exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// Removes the specified items from the cache. Any items not present in the cache are ignored.
    /// Produces a <b>Remove</b> changeset for each item that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="items">The <see cref="IEnumerable{TObject}"/> of items to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(items));
    }

    /// <summary>
    /// Removes the items with the specified keys from the cache. Any keys not present are ignored.
    /// Produces a <b>Remove</b> changeset for each key that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="keys">The <see cref="IEnumerable{TKey}"/> keys to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(keys));
    }

    /// <inheritdoc cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, TKey)"/>
    /// <param name="source">The <see cref="IIntermediateCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <inheritdoc cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TKey})"/>
    /// <param name="source">The <see cref="IIntermediateCache{TObject, TKey}"/> from which to remove items.</param>
    /// <param name="keys">The <see cref="IEnumerable{TKey}"/> keys to remove.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(keys));
    }

    /// <summary>
    /// Strips the key from a cache changeset, converting <see cref="IChangeSet{TObject, TKey}"/> to
    /// <see cref="IChangeSet{TObject}"/> (list changeset). All indexed changes are dropped (sorting is not supported).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to strip keys from, producing an unkeyed list changeset.</param>
    /// <returns>A list changeset stream without key information.</returns>
    /// <seealso cref="ObservableListEx.AddKey{TObject, TKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TKey})"/>
    /// <seealso cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    public static IObservable<IChangeSet<TObject>> RemoveKey<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(
            changes =>
            {
                var enumerator = new RemoveKeyEnumerator<TObject, TKey>(changes);
                return new ChangeSet<TObject>(enumerator);
            });
    }

    /// <summary>
    /// Removes a specific key from the cache. Equivalent to <c>source.Edit(u =&gt; u.RemoveKey(key))</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove a key.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void RemoveKey<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.RemoveKey(key));
    }

    /// <summary>
    /// Removes multiple keys from the cache in a single <c>Edit</c> call. Keys not present in the cache are ignored.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> from which to remove keys.</param>
    /// <param name="keys">The <see cref="IEnumerable{TKey}"/> keys to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.RemoveKeys(keys));
    }

    /// <summary>
    /// Sets the <c>Index</c> property on each item (which must implement <see cref="IIndexAware"/>)
    /// to reflect its position in the sorted output. Operates on <see cref="ISortedChangeSet{TObject, TKey}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to update index positions in.</param>
    /// <returns>An observable that emits the sorted changesets after updating item indices.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : IIndexAware
        where TKey : notnull => source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));
}
