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
    /// Prepends an empty changeset to the source stream, ensuring subscribers always receive an immediate
    /// (empty) notification on subscription. Uses Rx's <c>StartWith</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty changeset first, then all source changesets.</returns>
    /// <seealso cref="ObservableListEx.StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(ChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty sorted changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="ISortedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(SortedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{IVirtualChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty virtual changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IVirtualChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{IPagedChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty paged changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IPagedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(PagedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IGroupChangeSet{TObject, TKey, TGroupKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IImmutableGroupChangeSet{TObject, TKey, TGroupKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty immutable group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IImmutableGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IReadOnlyCollection{T}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty collection first, then all source collections.</returns>
    /// <remarks>Overload for <see cref="IReadOnlyCollection{T}"/>.</remarks>
    public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source) => source.StartWith(ReadOnlyCollectionLight<T>.Empty);
}
