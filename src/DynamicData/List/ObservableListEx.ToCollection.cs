// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Emits the full collection as an <see cref="IReadOnlyCollection{T}"/> after every changeset. Equivalent to <c>QueryWhenChanged(items => items)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a collection on each change.</param>
    /// <returns>An observable emitting the full collection snapshot after each change.</returns>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ObservableCacheEx.ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.QueryWhenChanged(items => items);
}
