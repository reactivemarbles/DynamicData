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
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Strips the key from a cache changeset, converting <see cref="IChangeSet{TObject, TKey}"/> to
    /// <see cref="IChangeSet{TObject}"/> (list changeset). Cache keys are tracked to supply known list indexes,
    /// keeping entries with equal values at distinct positions.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to strip keys from, producing an unkeyed list changeset.</param>
    /// <returns>A list changeset stream without key information.</returns>
    /// <remarks>
    /// Supplied addition, update, removal, and move indexes are preserved. Additions retain unspecified indexes when supplied,
    /// including the addition produced by an update; their append positions are tracked internally when known.
    /// Updates produce a removal followed by an addition; refreshes produce a replacement of the item with itself.
    /// Partial streams retain unspecified indexes where positions cannot be inferred. An unindexed removal or update
    /// of an untracked key invalidates inferred positions; later indexed changes can establish known positions again.
    /// </remarks>
    /// <seealso cref="ObservableListEx.AddKey{TObject, TKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TKey})"/>
    /// <seealso cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    public static IObservable<IChangeSet<TObject>> RemoveKey<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Defer(
            () =>
            {
                // Store only observed positions, not placeholders for unobserved items in a partial stream.
                // Mirror known positions by key so refresh lookup does not scan the ordered list.
                var keys = new List<ItemWithIndex<TKey>>();
                var indexesByKey = new Dictionary<TKey, int>();
                var canInferAppendIndex = true;

                return source.Select(
                    changes =>
                    {
                        // Preserve individual change reasons rather than coalescing them into ranges or Clear.
                        var result = new ChangeSet<TObject>(changes.Count + changes.Updates);

                        foreach (var change in changes.ToConcreteType())
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    {
                                        InsertKey(change.Key, change.CurrentIndex);
                                        result.Add(new Change<TObject>(ListChangeReason.Add, change.Current, change.CurrentIndex));
                                    }

                                    break;

                                case ChangeReason.Refresh:
                                    {
                                        // Cache refresh indexes are not positional. Preserve the legacy unknown-index
                                        // self-replacement when this subscription has not observed the key's position.
                                        var index = FindIndex(change.Key);
                                        if (index < 0)
                                        {
                                            canInferAppendIndex = false;
                                        }

                                        result.Add(new Change<TObject>(ListChangeReason.Replace, change.Current, change.Current, index, index));
                                    }

                                    break;

                                case ChangeReason.Moved:
                                    RemoveKeyPosition(change.Key, change.PreviousIndex);
                                    InsertKey(change.Key, change.CurrentIndex);
                                    result.Add(new Change<TObject>(change.Current, change.CurrentIndex, change.PreviousIndex));
                                    break;

                                case ChangeReason.Update:
                                    {
                                        var previousIndex = RemoveKeyPosition(change.Key, change.PreviousIndex);
                                        result.Add(new Change<TObject>(ListChangeReason.Remove, change.Previous.Value, previousIndex));

                                        InsertKey(change.Key, change.CurrentIndex);
                                        result.Add(new Change<TObject>(ListChangeReason.Add, change.Current, change.CurrentIndex));
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    {
                                        var index = RemoveKeyPosition(change.Key, change.CurrentIndex);
                                        result.Add(new Change<TObject>(ListChangeReason.Remove, change.Current, index));
                                    }

                                    break;
                            }
                        }

                        return result;
                    });

                int FindIndex(TKey key)
                    => indexesByKey.TryGetValue(key, out var index) ? index : -1;

                void InsertKey(TKey key, int suppliedIndex)
                {
                    var index = suppliedIndex >= 0 ? suppliedIndex : canInferAppendIndex ? keys.Count : suppliedIndex;
                    if (index < 0)
                    {
                        // An append after an incomplete history has no known absolute position.
                        return;
                    }

                    if (index > keys.Count)
                    {
                        canInferAppendIndex = false;
                    }

                    var slot = keys.Count;
                    while (slot > 0 && keys[slot - 1].Index >= index)
                    {
                        --slot;
                        var item = keys[slot];
                        if (item.Index == int.MaxValue)
                        {
                            // The shifted position is not representable; do not invent a wrapped index.
                            keys.RemoveAt(slot);
                            indexesByKey.Remove(item.Item);
                            canInferAppendIndex = false;
                        }
                        else
                        {
                            var shiftedIndex = item.Index + 1;
                            keys[slot] = new ItemWithIndex<TKey>(item.Item, shiftedIndex);
                            indexesByKey[item.Item] = shiftedIndex;
                        }
                    }

                    keys.Insert(slot, new ItemWithIndex<TKey>(key, index));
                    indexesByKey[key] = index;
                }

                int RemoveKeyPosition(TKey key, int suppliedIndex)
                {
                    // Supplied positions in a complete stream can be read directly.
                    var knownIndex = canInferAppendIndex
                        && suppliedIndex >= 0
                        && suppliedIndex < keys.Count
                        && EqualityComparer<TKey>.Default.Equals(keys[suppliedIndex].Item, key)
                            ? suppliedIndex
                            : FindIndex(key);
                    var index = suppliedIndex >= 0 ? suppliedIndex : knownIndex >= 0 ? knownIndex : suppliedIndex;

                    if (knownIndex < 0)
                    {
                        canInferAppendIndex = false;
                    }

                    if (index < 0 || (knownIndex >= 0 && index != knownIndex))
                    {
                        // An unknown removal location, or a conflicting supplied index, makes subsequent
                        // inferred positions unsafe. Keep projecting the supplied change instead of throwing.
                        keys.Clear();
                        indexesByKey.Clear();
                        canInferAppendIndex = false;
                        return index;
                    }

                    for (var slot = keys.Count - 1; slot >= 0; --slot)
                    {
                        var item = keys[slot];
                        if (item.Index < index)
                        {
                            break;
                        }

                        if (item.Index == index)
                        {
                            if (knownIndex >= 0)
                            {
                                keys.RemoveAt(slot);
                                indexesByKey.Remove(item.Item);
                            }
                            else
                            {
                                // The supplied removal occupies another key's inferred position.
                                keys.Clear();
                                indexesByKey.Clear();
                                canInferAppendIndex = false;
                            }

                            break;
                        }

                        var shiftedIndex = item.Index - 1;
                        keys[slot] = new ItemWithIndex<TKey>(item.Item, shiftedIndex);
                        indexesByKey[item.Item] = shiftedIndex;
                    }

                    return index;
                }
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
}
