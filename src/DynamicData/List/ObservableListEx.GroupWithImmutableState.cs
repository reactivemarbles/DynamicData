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
    /// Groups source items by the value returned by <paramref name="groupSelectorKey"/>. Each update produces immutable grouping snapshots
    /// rather than live inner observable lists.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to group with immutable snapshots.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> function that returns the group key for each item.</param>
    /// <param name="regrouper">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> that forces all items to be re-evaluated when it fires.</param>
    /// <returns>A list changeset stream of <see cref="List.IGrouping{TObject, TGroupKey}"/> immutable snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="groupSelectorKey"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Works like <see cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// but each affected group emits a new immutable snapshot on every change rather than updating a live inner list.
    /// This is useful when consumers need thread-safe, point-in-time snapshots of each group.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> GroupWithImmutableState<TObject, TGroupKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOnImmutable<TObject, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }
}
