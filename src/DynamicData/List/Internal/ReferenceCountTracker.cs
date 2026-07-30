// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// <para>Ripped and adapted from https://clinq.codeplex.com/.</para>
/// <para>Thanks dudes.</para>
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
internal sealed class ReferenceCountTracker<T>
    where T : notnull
{
    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IEnumerable<T> Items => ReferenceCounts.Keys;

    /// <summary>
    /// Gets the ReferenceCounts value.
    /// </summary>
    private Dictionary<T, int> ReferenceCounts { get; } = [];

    /// <summary>
    /// Gets or sets the indexed value.
    /// </summary>
    /// <param name="item">The item value.</param>
    public int this[T item] => ReferenceCounts[item];

    /// <summary>
    ///     Increments the reference count for the item.  Returns true when reference count goes from 0 to 1.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>The result of the operation.</returns>
    public bool Add(T item)
    {
        ArgumentExceptionHelper.ThrowIfNull(item);

        if (!ReferenceCounts.TryGetValue(item, out var currentCount))
        {
            ReferenceCounts.Add(item, 1);
            return true;
        }

        ReferenceCounts[item] = currentCount + 1;
        return false;
    }

    /// <summary>
    /// Executes the Clear operation.
    /// </summary>
    public void Clear() => ReferenceCounts.Clear();

    /// <summary>
    /// Executes the Contains operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Contains(T item) => ReferenceCounts.ContainsKey(item);

    /// <summary>
    ///     Decrements the reference count for the item.  Returns true when reference count goes from 1 to 0.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>The result of the operation.</returns>
    public bool Remove(T item)
    {
        ArgumentExceptionHelper.ThrowIfNull(item);

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
