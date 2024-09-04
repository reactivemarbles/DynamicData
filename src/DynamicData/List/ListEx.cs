// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions to help with maintenance of a list.
/// </summary>
public static class ListEx
{
    /// <summary>
    /// Adds the  items to the specified list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// items.
    /// </exception>
    public static void Add<T>(this IList<T> source, IEnumerable<T> items)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        items.ForEach(source.Add);
    }

    /// <summary>
    /// Adds the range if a negative is specified, otherwise the range is added at the end of the list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <param name="index">The index.</param>
    public static void AddOrInsertRange<T>(this IList<T> source, IEnumerable<T> items, int index)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        switch (source)
        {
            case List<T> list when index >= 0:
                list.InsertRange(index, items);
                break;
            case List<T> list:
                list.AddRange(items);
                break;
            case IExtendedList<T> extendedList when index >= 0:
                extendedList.InsertRange(items, index);
                break;
            case IExtendedList<T> extendedList:
                extendedList.AddRange(items);
                break;
            default:
                {
                    if (index >= 0)
                    {
                        // TODO: Why the hell reverse? Surely there must be as reason otherwise I would not have done it.
                        items.Reverse().ForEach(t => source.Insert(index, t));
                    }
                    else
                    {
                        items.ForEach(source.Add);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// Adds the range to the source list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// items.
    /// </exception>
    public static void AddRange<T>(this IList<T> source, IEnumerable<T> items)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        switch (source)
        {
            case List<T> list:
                list.AddRange(items);
                break;
            case IExtendedList<T> extendedList:
                extendedList.AddRange(items);
                break;
            default:
                items.ForEach(source.Add);
                break;
        }
    }

    /// <summary>
    /// Adds the range to the list. The starting range is at the specified index.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <param name="index">The index.</param>
    public static void AddRange<T>(this IList<T> source, IEnumerable<T> items, int index)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        switch (source)
        {
            case List<T> list:
                list.InsertRange(index, items);
                break;
            case IExtendedList<T> list:
                list.InsertRange(items, index);
                break;
            default:
                items.ForEach(source.Add);
                break;
        }
    }

    /// <summary>
    /// Performs a binary search on the specified collection.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>The index of the specified value in the specified array, if value is found; otherwise, a negative number.</returns>
    public static int BinarySearch<TItem>(this IList<TItem> list, TItem value) => BinarySearch(list, value, Comparer<TItem>.Default);

    /// <summary>
    /// Performs a binary search on the specified collection.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
    /// <returns>The index of the specified value in the specified array, if value is found; otherwise, a negative number.</returns>
    public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
    {
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return list.BinarySearch(value, comparer.Compare);
    }

    /// <summary>
    /// <para>Performs a binary search on the specified collection.</para>
    /// <para>Thanks to https://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt.</para>
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TSearch">The type of the searched item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
    /// <returns>The index of the specified value in the specified array, if value is found; otherwise, a negative number.</returns>
    public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
    {
        list.ThrowArgumentNullExceptionIfNull(nameof(list));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        var lower = 0;
        var upper = list.Count - 1;

        while (lower <= upper)
        {
            var middle = lower + ((upper - lower) / 2);
            var comparisonResult = comparer(value, list[middle]);
            if (comparisonResult < 0)
            {
                upper = middle - 1;
            }
            else if (comparisonResult > 0)
            {
                lower = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return ~lower;
    }

    /// <summary>
    /// Clones the list from the specified change set.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="changes">The changes.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// changes.
    /// </exception>
    public static void Clone<T>(this IList<T> source, IChangeSet<T> changes)
        where T : notnull => Clone(source, changes, null);

    /// <summary>
    /// Clones the list from the specified change set.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="equalityComparer">An equality comparer to match items in the changes.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// changes.
    /// </exception>
    public static void Clone<T>(this IList<T> source, IChangeSet<T> changes, IEqualityComparer<T>? equalityComparer)
        where T : notnull => Clone(source, (IEnumerable<Change<T>>)changes, equalityComparer);

    /// <summary>
    /// Clones the list from the specified enumerable of changes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="equalityComparer">An equality comparer to match items in the changes.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// changes.
    /// </exception>
    public static void Clone<T>(this IList<T> source, IEnumerable<Change<T>> changes, IEqualityComparer<T>? equalityComparer)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        foreach (var item in changes)
        {
            Clone(source, item, equalityComparer ?? EqualityComparer<T>.Default);
        }
    }

    /// <summary>
    /// Finds the index of the current item using the specified equality comparer.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="item">The item to get the index of.</param>
    /// <returns>The index.</returns>
    public static int IndexOf<T>(this IEnumerable<T> source, T item) => IndexOf(source, item, EqualityComparer<T>.Default);

    /// <summary>
    /// Finds the index of the current item using the specified equality comparer.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="item">The item to get the index of.</param>
    /// <param name="equalityComparer">Use to determine object equality.</param>
    /// <returns>The index.</returns>
    public static int IndexOf<T>(this IEnumerable<T> source, T item, IEqualityComparer<T> equalityComparer)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        var i = 0;
        foreach (var candidate in source)
        {
            if (equalityComparer.Equals(item, candidate))
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    /// <summary>
    /// Lookups the item using the specified comparer. If matched, the item's index is also returned.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="item">The item.</param>
    /// <param name="equalityComparer">The equality comparer.</param>
    /// <returns>The index of the item if available.</returns>
    public static Optional<ItemWithIndex<T>> IndexOfOptional<T>(this IEnumerable<T> source, T item, IEqualityComparer<T>? equalityComparer = null)
    {
        var comparer = equalityComparer ?? EqualityComparer<T>.Default;
        var index = source.IndexOf(item, comparer);
        return index < 0 ? Optional<ItemWithIndex<T>>.None : new ItemWithIndex<T>(item, index);
    }

    /// <summary>
    /// Removes the  items from the specified list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// items.
    /// </exception>
    public static void Remove<T>(this IList<T> source, IEnumerable<T> items)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        items.ForEach(t => source.Remove(t));
    }

    /// <summary>
    /// Removes many items from the collection in an optimal way.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="itemsToRemove">The items to remove.</param>
    public static void RemoveMany<T>(this IList<T> source, IEnumerable<T> itemsToRemove)
    {
        /*
            This may seem OTT but for large sets of data where there are many removes scattered
            across the source collection IndexOf lookups can result in very slow updates
            (especially for subsequent operators)
        */
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        itemsToRemove.ThrowArgumentNullExceptionIfNull(nameof(itemsToRemove));

        var toRemoveArray = itemsToRemove.AsArray();

        // match all indexes and and remove in reverse as it is more efficient
        var toRemove = source.IndexOfMany(toRemoveArray).OrderByDescending(x => x.Index).ToArray();

        // if there are duplicates, it could be that an item exists in the
        // source collection more than once - in that case the fast remove
        // would remove each instance
        var hasDuplicates = toRemove.Duplicates(t => t.Item).Any();

        if (hasDuplicates)
        {
            // Slow remove but safe
            toRemoveArray.ForEach(t => source.Remove(t));
        }
        else
        {
            // Fast remove because we know the index of all and we remove in order
            toRemove.ForEach(t => source.RemoveAt(t.Index));
        }
    }

    /// <summary>
    /// Replaces the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="original">The original.</param>
    /// <param name="replaceWith">The value to replace with.</param>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// items.</exception>
    public static void Replace<T>(this IList<T> source, T original, T replaceWith)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        original.ThrowArgumentNullExceptionIfNull(nameof(original));
        replaceWith.ThrowArgumentNullExceptionIfNull(nameof(replaceWith));

        var index = source.IndexOf(original);
        if (index == -1)
        {
            throw new ArgumentException("Cannot find index of original item. Either it does not exist in the list or the hashcode has mutated");
        }

        source[index] = replaceWith;
    }

    /// <summary>
    /// Replaces the specified item.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="original">The item which is to be replaced. If not in the list and argument exception will be thrown.</param>
    /// <param name="replaceWith">The new item.</param>
    /// <param name="comparer">The equality comparer to be used to find the original item in the list.</param>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// items.</exception>
    public static void Replace<T>(this IList<T> source, T original, T replaceWith, IEqualityComparer<T> comparer)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        original.ThrowArgumentNullExceptionIfNull(nameof(original));
        replaceWith.ThrowArgumentNullExceptionIfNull(nameof(replaceWith));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        var index = source.IndexOf(original);
        if (index == -1)
        {
            throw new ArgumentException("Cannot find index of original item. Either it does not exist in the list or the hashcode has mutated");
        }

        if (comparer.Equals(source[index], replaceWith))
        {
            source[index] = replaceWith;
        }
    }

    /// <summary>
    /// Replaces the item if found, otherwise the item is added to the list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="original">The original.</param>
    /// <param name="replaceWith">The value to replace with.</param>
    public static void ReplaceOrAdd<T>(this IList<T> source, T original, T replaceWith)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        original.ThrowArgumentNullExceptionIfNull(nameof(original));
        replaceWith.ThrowArgumentNullExceptionIfNull(nameof(replaceWith));

        var index = source.IndexOf(original);
        if (index == -1)
        {
            source.Add(replaceWith);
        }
        else
        {
            source[index] = replaceWith;
        }
    }

    /// <summary>
    /// <para>Clears the collection if the number of items in the range is the same as the source collection. Otherwise a  remove many operation is applied.</para>
    /// <para>NB: This is because an observable change set may be a composite of multiple change sets in which case if one of them has clear operation applied it should not clear the entire result.</para>
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="change">The change.</param>
    internal static void ClearOrRemoveMany<T>(this IList<T> source, Change<T> change)
        where T : notnull
    {
        // apply this to other operators
        if (source.Count == change.Range.Count)
        {
            source.Clear();
        }
        else
        {
            source.RemoveMany(change.Range);
        }
    }

    internal static bool MovedWithinRange<T>(this Change<T> source, int startIndex, int endIndex)
        where T : notnull
    {
        if (source.Reason != ListChangeReason.Moved)
        {
            return false;
        }

        var current = source.Item.CurrentIndex;
        var previous = source.Item.PreviousIndex;

        return (current >= startIndex && current <= endIndex) || (previous >= startIndex && previous <= endIndex);
    }

    private static void Clone<T>(this IList<T> source, Change<T> item, IEqualityComparer<T> equalityComparer)
        where T : notnull
    {
        var changeAware = source as ChangeAwareList<T>;

        switch (item.Reason)
        {
            case ListChangeReason.Add:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.Insert(change.CurrentIndex, change.Current);
                    }
                    else
                    {
                        source.Add(change.Current);
                    }

                    break;
                }

            case ListChangeReason.AddRange:
                {
                    source.AddOrInsertRange(item.Range, item.Range.Index);
                    break;
                }

            case ListChangeReason.Clear:
                {
                    source.ClearOrRemoveMany(item);
                    break;
                }

            case ListChangeReason.Replace:
                {
                    var change = item.Item;
                    if (change.CurrentIndex >= 0 && change.CurrentIndex == change.PreviousIndex)
                    {
                        source[change.CurrentIndex] = change.Current;
                    }
                    else
                    {
                        if (change.PreviousIndex == -1)
                        {
                            source.Remove(change.Previous.Value);
                        }
                        else
                        {
                            // is this best? or replace + move?
                            source.RemoveAt(change.PreviousIndex);
                        }

                        if (change.CurrentIndex == -1)
                        {
                            source.Add(change.Current);
                        }
                        else
                        {
                            source.Insert(change.CurrentIndex, change.Current);
                        }
                    }

                    break;
                }

            case ListChangeReason.Refresh:
                {
                    if (changeAware is not null)
                    {
                        changeAware.RefreshAt(item.Item.CurrentIndex);
                    }
                    else
                    {
                        source.RemoveAt(item.Item.CurrentIndex);
                        source.Insert(item.Item.CurrentIndex, item.Item.Current);
                    }

                    break;
                }

            case ListChangeReason.Remove:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        var index = source.IndexOf(change.Current, equalityComparer);
                        if (index > -1)
                        {
                            source.RemoveAt(index);
                        }
                    }

                    break;
                }

            case ListChangeReason.RemoveRange:
                {
                    // ignore this case because WhereReasonsAre removes the index [in which case call RemoveMany]
                    //// if (item.Range.Index < 0)
                    ////    throw new UnspecifiedIndexException("ListChangeReason.RemoveRange should not have an index specified index");
                    if (item.Range.Index >= 0 && (source is IExtendedList<T> || source is List<T>))
                    {
                        source.RemoveRange(item.Range.Index, item.Range.Count);
                    }
                    else
                    {
                        source.RemoveMany(item.Range);
                    }

                    break;
                }

            case ListChangeReason.Moved:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (!hasIndex)
                    {
                        throw new UnspecifiedIndexException("Cannot move as an index was not specified");
                    }

                    if (source is IExtendedList<T> extendedList)
                    {
                        extendedList.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else if (source is ObservableCollection<T> observableCollection)
                    {
                        observableCollection.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else
                    {
                        // check this works whatever the index is
                        source.RemoveAt(change.PreviousIndex);
                        source.Insert(change.CurrentIndex, change.Current);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// Removes the number of items, starting at the specified index.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    /// <exception cref="NotSupportedException">Cannot remove range.</exception>
    private static void RemoveRange<T>(this IList<T> source, int index, int count)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        switch (source)
        {
            case List<T> list:
                list.RemoveRange(index, count);
                break;
            case IExtendedList<T> list:
                list.RemoveRange(index, count);
                break;
            default:
                throw new NotSupportedException($"Cannot remove range from {source.GetType().FullName}");
        }
    }
}
