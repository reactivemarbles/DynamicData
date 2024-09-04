// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal static class SortExtensions
{
    public static int GetCurrentPosition<TItem>(this IList<TItem> source, TItem item, IComparer<TItem> comparer, bool useBinarySearch = false)
    {
        var index = useBinarySearch ? source.BinarySearch(item, comparer) : source.IndexOf(item);

        if (index < 0)
        {
            throw new SortException($"Cannot find item: {typeof(TItem).Name} -> {item} from {source.Count} items");
        }

        return index;
    }

    public static int GetInsertPosition<T>(this IList<T> source, T item, IComparer<T> comparer, bool useBinarySearch = false)
    {
        return useBinarySearch
            ? source.GetInsertPositionBinary(item, comparer)
            : source.GetInsertPositionLinear(item, comparer);
    }

    public static int GetInsertPositionBinary<TItem>(this IList<TItem> list, TItem t, IComparer<TItem> c)
    {
        var index = list.BinarySearch(t, c);
        var insertIndex = ~index;

        // sort is not returning uniqueness
        if (insertIndex < 0)
        {
            throw new SortException("Binary search has been specified, yet the sort does not yield uniqueness");
        }

        return insertIndex;
    }

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
}
