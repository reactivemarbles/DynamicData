// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted by the value returned by <paramref name="sort"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TSortKey">The type of the sort key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="sort">A <see cref="Func{T, TResult}"/> function extracting the sort key from each item.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort direction. Defaults to ascending.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject}(IObservable{IChangeSet{TObject}}, IComparer{TObject})"/>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.ToSortedCollection{TObject, TKey}"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));

    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted using the specified <paramref name="comparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> used for sorting.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source, IComparer<TObject> comparer)
        where TObject : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });
}
