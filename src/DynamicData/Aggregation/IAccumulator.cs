// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Folds the projected values of a collection into an aggregate, one value at a time.
/// </summary>
/// <typeparam name="TValue">The type of the projected values.</typeparam>
/// <remarks>
/// <see cref="Remove"/> and <see cref="Replace"/> return <see langword="false"/> when the aggregate can no longer be derived
/// from the values seen so far, such as when the current maximum leaves the collection. The caller then calls
/// <see cref="Reset"/> and adds every remaining value again once the change set has been applied.
/// </remarks>
internal interface IAccumulator<in TValue>
{
    /// <summary>
    /// Includes a value in the aggregate.
    /// </summary>
    /// <param name="value">The value to include.</param>
    void Add(TValue value);

    /// <summary>
    /// Excludes a previously added value from the aggregate.
    /// </summary>
    /// <param name="value">The value to exclude.</param>
    /// <returns><see langword="false"/> when the aggregate must be rebuilt from the remaining values.</returns>
    bool Remove(TValue value);

    /// <summary>
    /// Replaces a previously added value with a new one.
    /// </summary>
    /// <param name="previous">The value to exclude.</param>
    /// <param name="current">The value to include.</param>
    /// <returns><see langword="false"/> when the aggregate must be rebuilt from the remaining values.</returns>
    bool Replace(TValue previous, TValue current);

    /// <summary>
    /// Returns the aggregate to its empty state.
    /// </summary>
    void Reset();
}
