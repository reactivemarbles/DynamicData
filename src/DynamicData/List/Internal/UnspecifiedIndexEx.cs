// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

/// <summary>
/// Resolves unspecified (-1) list change indexes against an operator's positional copy of its upstream list.
/// </summary>
/// <remarks>
/// <para>
/// An unspecified index locates an existing item by <see cref="EqualityComparer{T}.Default"/>, taking the first match.
/// An unspecified insertion index appends. A <see cref="ListChangeReason.Replace"/> with an unspecified previous index
/// locates its previous item that way, and an unspecified current index takes the previous item's position, so the
/// replacement happens in place. <see cref="ListEx.Clone{T}(IList{T}, IChangeSet{T})"/> applies the same rules, so every
/// positional copy of a list agrees on where an unindexed change lands.
/// </para>
/// <para>
/// The upstream copy is read through its indexer and a projection, so a lookup allocates nothing and does not
/// enumerate a <see cref="ChangeAwareList{T}"/>, whose enumerator copies the list.
/// </para>
/// </remarks>
internal static class UnspecifiedIndexEx
{
    /// <summary>
    /// Finds the first element of <paramref name="upstream"/> whose projected item equals <paramref name="item"/>.
    /// </summary>
    /// <typeparam name="TElement">The type of element held for each upstream item.</typeparam>
    /// <typeparam name="T">The type of the upstream item.</typeparam>
    /// <param name="upstream">The operator's positional copy of its upstream list.</param>
    /// <param name="item">The item to locate.</param>
    /// <param name="selectItem">Projects an element to the upstream item it holds.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> that matches items, or <see langword="null"/> for <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <returns>The index of the first match, or -1 when no element matches.</returns>
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

    /// <summary>
    /// Returns <paramref name="change"/> with every unspecified index replaced by the position it refers to in <paramref name="upstream"/>.
    /// </summary>
    /// <typeparam name="TElement">The type of element held for each upstream item.</typeparam>
    /// <typeparam name="T">The type of the upstream item.</typeparam>
    /// <param name="change">The change to resolve, before it has been applied to <paramref name="upstream"/>.</param>
    /// <param name="upstream">The operator's positional copy of its upstream list.</param>
    /// <param name="selectItem">Projects an element to the upstream item it holds.</param>
    /// <returns>
    /// The resolved change. A <see cref="ListChangeReason.Remove"/> of an item that is not present keeps an index of -1,
    /// and the caller ignores it, as <see cref="ListEx.Clone{T}(IList{T}, IChangeSet{T})"/> does.
    /// </returns>
    /// <exception cref="InvalidOperationException">An unindexed <see cref="ListChangeReason.Replace"/> names a previous item that is not present.</exception>
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
            ? throw new InvalidOperationException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list")
            : new(ListChangeReason.Replace, change.Current, change.Previous, (change.CurrentIndex < 0) ? previousIndex : change.CurrentIndex, previousIndex);
}
