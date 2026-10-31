// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Tracks the maximum or minimum of the values in a collection.
/// </summary>
/// <typeparam name="TValue">The type of the projected values.</typeparam>
/// <remarks>
/// Adding a value, or replacing one with a value at or beyond the extreme, updates the extreme directly. Removing the value
/// that holds the extreme, or replacing it with a value inside the range, requests a rebuild from the remaining values.
/// </remarks>
internal struct ExtremeAccumulator<TValue> : IAccumulator<TValue>
    where TValue : struct, IComparable<TValue>
{
    private readonly bool _isMaximum;

    private ExtremeAccumulator(bool isMaximum) => _isMaximum = isMaximum;

    /// <summary>
    /// Gets the current extreme, or <see langword="null"/> when no values have been added.
    /// </summary>
    public TValue? Extreme { get; private set; }

    public static ExtremeAccumulator<TValue> CreateMaximum() => new(isMaximum: true);

    public static ExtremeAccumulator<TValue> CreateMinimum() => new(isMaximum: false);

    public void Add(TValue value)
    {
        if (Extreme is not { } extreme || IsBeyond(value, extreme))
        {
            Extreme = value;
        }
    }

    public readonly bool Remove(TValue value) => Extreme is not { } extreme || value.CompareTo(extreme) != 0;

    public bool Replace(TValue previous, TValue current)
    {
        if (Extreme is not { } extreme || !IsBeyond(extreme, current))
        {
            Extreme = current;
            return true;
        }

        return previous.CompareTo(extreme) != 0;
    }

    public void Reset() => Extreme = null;

    private readonly bool IsBeyond(TValue value, TValue extreme)
    {
        var comparison = value.CompareTo(extreme);
        return _isMaximum ? comparison > 0 : comparison < 0;
    }
}
