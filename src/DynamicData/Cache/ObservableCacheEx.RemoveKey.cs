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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Strips the key from a cache changeset, converting <see cref="IChangeSet{TObject, TKey}"/> to
    /// <see cref="IChangeSet{TObject}"/> (list changeset). Positions are only carried through when the source supplies them,
    /// as a sorted source does; changes from an unsorted source carry an unspecified index of -1.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to strip keys from, producing an unkeyed list changeset.</param>
    /// <returns>A list changeset stream without key information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each changeset is projected independently and no state is kept, so the operator cannot know where an item sits in
    /// the list it produces. A cache does not order its items, so changes from an unsorted source are emitted with an
    /// unspecified index of -1. Downstream list operators locate such an item with <see cref="EqualityComparer{T}.Default"/>,
    /// taking the first equal item, the same way <see cref="ListEx.Clone{T}(IList{T}, IChangeSet{T})"/> does.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>An <b>Add</b> is emitted at the source's index, or -1 when the source is unsorted.</description></item>
    /// <item><term>Update</term><description>A <b>Remove</b> of the previous item followed by an <b>Add</b> of the current item, each at the source's index or -1.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted at the source's index, or -1 when the source is unsorted.</description></item>
    /// <item><term>Refresh</term><description>A <b>Replace</b> of the item with itself is emitted, with both indexes -1, because a list <b>Refresh</b> requires an index.</description></item>
    /// <item><term>Moved</term><description>A <b>Moved</b> is emitted with the source's indexes.</description></item>
    /// <item><term>OnError</term><description>Forwarded.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> once the key is gone, items are identified only by equality. Keep <see cref="object.Equals(object)"/>
    /// consistent with the cache key, so that two keys never hold equal items, and apply filters, transforms, and refresh
    /// handling in the cache before calling <see cref="RemoveKey{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>,
    /// where the key still identifies each item. Equal items held under different keys are interchangeable downstream, so an
    /// unindexed change to one of them may be applied to whichever comes first.
    /// </para>
    /// </remarks>
    /// <seealso cref="ObservableListEx.AddKey{TObject, TKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TKey})"/>
    /// <seealso cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    /// <seealso cref="ObservableListEx.RemoveIndex{T}(IObservable{IChangeSet{T}})"/>
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
}
