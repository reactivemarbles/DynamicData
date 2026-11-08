// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

// Resolves unspecified (-1) list change indexes against an operator's positional copy of its upstream list.
//
// An unspecified index locates an existing item by EqualityComparer<T>.Default, taking the first match. An unspecified
// insertion index appends. A Replace with an unspecified previous index locates its previous item that way, and an
// unspecified current index takes the previous item's position, so the replacement happens in place. ListEx.Clone
// applies the same rules, so every positional copy of a list agrees on where an unindexed change lands.
//
// The upstream copy is read through its indexer and a projection, so a lookup allocates nothing and does not enumerate
// a ChangeAwareList<T>, whose enumerator copies the list.
internal static class UnspecifiedIndexEx
{
    public static int IndexOf<TElement, T>(this IList<TElement> upstream, T item, Func<TElement, T> selectItem, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;

        for (var i = 0; i < upstream.Count; ++i)
        {
            if (comparer.Equals(selectItem(upstream[i]), item))
                return i;
        }

        return -1;
    }

    // Call before the change is applied to upstream. An unindexed Remove of an absent item keeps an index of -1 for the
    // caller to ignore, as ListEx.Clone does. An unindexed Replace of an absent item throws UnspecifiedIndexException.
    public static ItemChange<T> ResolveIndexes<TElement, T>(this ItemChange<T> change, IList<TElement> upstream, Func<TElement, T> selectItem)
        where T : notnull
        => change.Reason switch
        {
            ListChangeReason.Add when change.CurrentIndex < 0 => new(ListChangeReason.Add, change.Current, upstream.Count),
            ListChangeReason.Remove when change.CurrentIndex < 0 => new(ListChangeReason.Remove, change.Current, upstream.IndexOf(change.Current, selectItem)),
            ListChangeReason.Replace when (change.CurrentIndex < 0) || (change.PreviousIndex < 0) => ResolveReplace(change, (change.PreviousIndex < 0) ? upstream.IndexOf(change.Previous.Value, selectItem) : change.PreviousIndex),
            _ => change,
        };

    private static ItemChange<T> ResolveReplace<T>(ItemChange<T> change, int previousIndex)
        where T : notnull
        => (previousIndex < 0)
            ? throw new UnspecifiedIndexException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list")
            : new(ListChangeReason.Replace, change.Current, change.Previous, (change.CurrentIndex < 0) ? previousIndex : change.CurrentIndex, previousIndex);
}
