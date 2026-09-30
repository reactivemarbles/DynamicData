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
    /// <see cref="IChangeSet{TObject}"/> (list changeset). Cache keys are tracked to supply list indexes,
    /// keeping entries with equal values at distinct positions.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to strip keys from, producing an unkeyed list changeset.</param>
    /// <returns>A list changeset stream without key information.</returns>
    /// <remarks>
    /// Each subscription starts from an empty list and observes every change applied to it, including the contents of
    /// an already populated cache, which arrive through <see cref="IConnectableCache{TObject, TKey}.Connect"/> as
    /// additions. Under that contract the key identifies the entry and the tracked position is the position that entry
    /// occupies in the list this subscription produced, so entries holding equal values stay distinguishable. Supplied
    /// addition, update, removal, and move indexes are preserved. Updates produce a removal followed by an addition;
    /// refreshes produce a replacement of the item with itself.
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>An <b>Add</b> is emitted. A supplied index is used when it falls within the tracked list; otherwise the entry is appended and reported at the position it took.</description></item>
    /// <item><term>Update</term><description>A <b>Remove</b> of the previous item at its tracked position, followed by an <b>Add</b> of the current item. The key keeps its identity across the pair.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted at the position the key held, and the key stops being tracked.</description></item>
    /// <item><term>Refresh</term><description>A <b>Replace</b> of the item with itself is emitted, carrying the key's tracked position as both the previous and current index. No position changes.</description></item>
    /// <item><term>Moved</term><description>A <b>Moved</b> is emitted, carrying the position the key held and the position it now holds.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer. The tracked positions are discarded with the subscription.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para>
    /// A stream that omits part of its history, such as one taken through
    /// <see cref="Preview{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/> without the preceding additions, is
    /// outside this contract. Positions cannot be recovered from information the stream never carried, so no attempt is
    /// made to reconcile one: a change that contradicts the observed state reports an unknown index for that change
    /// alone and the remaining positions are left untouched.
    /// </para>
    /// <para><b>Worth noting:</b> position tracking is created per subscription and starts empty, so two subscribers
    /// taken at different times each report positions in the list they themselves produced, and a resubscription
    /// begins again from an empty list.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
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
                // Positions are derived on demand, so a removal never rewrites the positions of the entries that follow it.
                var positions = new KeyPositionIndex<TKey>();

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
                                    result.Add(new Change<TObject>(ListChangeReason.Add, change.Current, Insert(change.Key, change.CurrentIndex)));
                                    break;

                                case ChangeReason.Refresh:
                                    {
                                        // A cache refresh carries no position, so the key's tracked position is the only
                                        // way to identify the entry.
                                        var index = Locate(change.Key);
                                        result.Add(new Change<TObject>(ListChangeReason.Replace, change.Current, change.Current, index, index));
                                    }

                                    break;

                                case ChangeReason.Moved:
                                    {
                                        // A move is only ever reported by an indexed source, which supplies both positions.
                                        var previousIndex = Extract(change.Key);
                                        var currentIndex = Insert(change.Key, change.CurrentIndex);
                                        result.Add(new Change<TObject>(change.Current, currentIndex, previousIndex));
                                    }

                                    break;

                                case ChangeReason.Update:
                                    result.Add(new Change<TObject>(ListChangeReason.Remove, change.Previous.Value, Extract(change.Key)));
                                    result.Add(new Change<TObject>(ListChangeReason.Add, change.Current, Insert(change.Key, change.CurrentIndex)));
                                    break;

                                case ChangeReason.Remove:
                                    result.Add(new Change<TObject>(ListChangeReason.Remove, change.Current, Extract(change.Key)));
                                    break;
                            }
                        }

                        return result;
                    });

                // The contract is a complete history from an empty list, so a key is tracked exactly when the list this
                // subscription produced holds it, and the tracked position is authoritative. A change that contradicts
                // that state is outside the contract: report the legacy unknown index for that change alone, leaving
                // every other position, which this subscription did emit, intact.
                int Locate(TKey key)
                    => positions.TryGetIndex(key, out var index) ? index : -1;

                int Insert(TKey key, int suppliedIndex)
                {
                    if (positions.Contains(key))
                        return -1;

                    // An unindexed source supplies no position, so a new entry belongs at the end of the list.
                    var index = suppliedIndex >= 0 && suppliedIndex <= positions.Count ? suppliedIndex : positions.Count;
                    positions.InsertAt(index, key);
                    return index;
                }

                int Extract(TKey key) => positions.Remove(key);
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
