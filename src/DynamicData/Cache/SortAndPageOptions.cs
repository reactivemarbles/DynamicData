// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace DynamicData;

/// <summary>
/// Options for the sort and virtualize operator.
/// </summary>
public record struct SortAndPageOptions()
{
    /// <summary>
    /// The sort reset threshold ie the number of changes before a reset is fired.
    /// </summary>
    public int ResetThreshold { get; init; } = BindingOptions.DefaultResetThreshold;

    /// <summary>
    /// Use binary search when the result of the comparer is a pure function.
    /// </summary>
    public bool UseBinarySearch { get; init; }

    /// <summary>
    /// Set the initial capacity of internal sorted list.
    /// </summary>
    public int InitialCapacity { get; init; }
}
