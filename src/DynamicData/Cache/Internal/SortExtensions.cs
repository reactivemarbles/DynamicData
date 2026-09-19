// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the SortExtensions class.
/// </summary>
internal static class SortExtensions
{
    /// <summary>
    /// Executes the GetCurrentPosition operation.
    /// </summary>
    /// <typeparam name="TItem">The type of the TItem value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="useBinarySearch">The useBinarySearch value.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetCurrentPosition<TItem>(this IList<TItem> source, TItem item, IComparer<TItem> comparer, bool useBinarySearch = false)
    {
        var index = useBinarySearch ? source.BinarySearch(item, comparer) : source.IndexOf(item);

        if (index < 0)
        {
            throw new SortException($"Cannot find item: {typeof(TItem).Name} -> {item} from {source.Count} items");
        }

        return index;
    }

    /// <summary>
    /// Executes the GetInsertPosition operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="useBinarySearch">The useBinarySearch value.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetInsertPosition<T>(this IList<T> source, T item, IComparer<T> comparer, bool useBinarySearch = false)
    {
        return useBinarySearch
            ? source.GetInsertPositionBinary(item, comparer)
            : source.GetInsertPositionLinear(item, comparer);
    }

    /// <summary>
    /// Executes the GetInsertPositionBinary operation.
    /// </summary>
    /// <typeparam name="TItem">The type of the TItem value.</typeparam>
    /// <param name="list">The list value.</param>
    /// <param name="t">The t value.</param>
    /// <param name="c">The c value.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetInsertPositionBinary<TItem>(this IList<TItem> list, TItem t, IComparer<TItem> c)
    {
        var index = list.BinarySearch(t, c);
        var insertIndex = ~index;

        // sort is not returning uniqueness
        if (insertIndex < 0)
        {
            /*
             * Binary search should not strictly already contain the item (or an item with the same value) when
             * attempting to find the insert position (it does for updates).  This can result in the insert position not being found.
             * In this case revert to linear search.
             */
            index = list.GetInsertPositionLinear(t, c);
            if (index >= 0)
            {
                return index;
            }

            throw new SortException("Binary search has been specified, yet the sort does not yield uniqueness");
        }

        return insertIndex;
    }

    /// <summary>
    /// Executes the GetInsertPositionLinear operation.
    /// </summary>
    /// <typeparam name="TItem">The type of the TItem value.</typeparam>
    /// <param name="list">The list value.</param>
    /// <param name="t">The t value.</param>
    /// <param name="c">The c value.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetInsertPositionLinear<TItem>(this IList<TItem> list, TItem t, IComparer<TItem> c)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (c.Compare(t, list[i]) < 0)
            {
                return i;
            }
        }

        return list.Count;
    }

    /// <summary>
    /// Executes the Move operation.
    /// </summary>
    /// <typeparam name="TItem">The type of the TItem value.</typeparam>
    /// <param name="list">The list value.</param>
    /// <param name="original">The original value.</param>
    /// <param name="destination">The destination value.</param>
    /// <param name="item">The item value.</param>
    public static void Move<TItem>(this IList<TItem> list, int original, int destination, TItem item)
    {
        // If the list supports the Move method, use it instead of removing and inserting.
        if (list is IExtendedList<TItem> extendedList)
        {
            extendedList.Move(original, destination);
        }
        else if (list is ObservableCollection<TItem> observableList)
        {
            observableList.Move(original, destination);
        }
        else
        {
            list.RemoveAt(original);
            list.Insert(destination, item);
        }
    }
}
