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
}
