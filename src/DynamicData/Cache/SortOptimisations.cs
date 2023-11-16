// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Flags used to specify one or more sort optimisations.
/// </summary>
[Flags]
public enum SortOptimisations
{
    /// <summary>
    /// No sorting optimisation are applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Specify this option if the comparer used for sorting compares immutable fields only.
    /// In which case index changes can be calculated using BinarySearch rather than the expensive IndexOf.
    /// </summary>
    ComparesImmutableValuesOnly = 1,

    /// <summary>
    /// Ignores moves because of evaluates.
    /// Use for virtualisatiom or pagination.
    /// </summary>
    IgnoreEvaluates = 2,

    /// <summary>
    /// The insert at end then sort entire set.  This can be the best algorithm for large data sets with many changes.
    /// </summary>
    [Obsolete("This is no longer being used. Use one of the other options instead.")]
    InsertAtEndThenSort = 3
}
