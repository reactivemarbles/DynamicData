// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

/// <summary>
/// <para>Ripped and adapted from https://clinq.codeplex.com/.</para>
/// <para>Thanks dudes.</para>
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
internal sealed class ReferenceCountTracker<T>
    where T : notnull
{
    public IEnumerable<T> Items => ReferenceCounts.Keys;

    private Dictionary<T, int> ReferenceCounts { get; } = [];

    public int this[T item] => ReferenceCounts[item];

    /// <summary>
    ///     Increments the reference count for the item.  Returns true when reference count goes from 0 to 1.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public bool Add(T item)
    {
        item.ThrowArgumentNullExceptionIfNull(nameof(item));

        if (!ReferenceCounts.TryGetValue(item, out var currentCount))
        {
            ReferenceCounts.Add(item, 1);
            return true;
        }

        ReferenceCounts[item] = currentCount + 1;
        return false;
    }

    public void Clear() => ReferenceCounts.Clear();

    public bool Contains(T item) => ReferenceCounts.ContainsKey(item);

    /// <summary>
    ///     Decrements the reference count for the item.  Returns true when reference count goes from 1 to 0.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    public bool Remove(T item)
    {
        item.ThrowArgumentNullExceptionIfNull(nameof(item));

        var currentCount = ReferenceCounts[item];

        if (currentCount == 1)
        {
            ReferenceCounts.Remove(item);
            return true;
        }

        ReferenceCounts[item] = currentCount - 1;
        return false;
    }
}
