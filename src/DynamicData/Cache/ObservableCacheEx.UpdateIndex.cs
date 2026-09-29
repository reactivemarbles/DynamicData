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
