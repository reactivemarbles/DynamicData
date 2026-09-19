// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Emits a sorted <c>IReadOnlyCollection&lt;T&gt;</c> after every changeset, sorted by the value returned by <paramref name="sort"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TSortKey">The type of the sort key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to materialize into a sorted collection on each change.</param>
    /// <param name="sort">A <c>Func&lt;T, TResult&gt;</c> function extracting the sort key from each item.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort direction. Defaults to ascending.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso><c>ToCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;)</c></seealso>
    /// <seealso><c>ToSortedCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, IComparer&lt;TObject&gt;)</c></seealso>
    /// <seealso><c>QueryWhenChanged&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.ToSortedCollection&lt;TObject, TKey&gt;</c></seealso>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));

    /// <summary>
    /// Emits a sorted <c>IReadOnlyCollection&lt;T&gt;</c> after every changeset, sorted using the specified <paramref name="comparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to materialize into a sorted collection on each change.</param>
    /// <param name="comparer">The <c>IComparer&lt;TObject&gt;</c> used for sorting.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso><c>ToSortedCollection&lt;TObject, TSortKey&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TSortKey&gt;, SortDirection)</c></seealso>
    /// <seealso><c>ToCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;)</c></seealso>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source, IComparer<TObject> comparer)
        where TObject : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });
}
