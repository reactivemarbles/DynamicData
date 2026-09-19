// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Specifies which filter strategy should be used when the filter predicate is changed.
/// </summary>
public enum ListFilterPolicy
{
    /// <summary>
    /// <para>Clears all items and replaces with the new matches. Preserves order.</para>
    /// <para>
    /// Useful when downstream consumers (such as UI bindings) handle full resets more efficiently than individual
    /// Add/Remove changes, or when the change set is expected to be very large relative to the source.
    /// </para>
    /// </summary>
    ClearAndReplace,

    /// <summary>
    /// <para>Calculates the minimal diff between the previous and new result sets. Does not preserve order.</para>
    /// <para>
    /// Generally preferred for performance: only items whose inclusion status actually changed produce an Add or Remove.
    /// Recommended for most scenarios.
    /// </para>
    /// </summary>
    CalculateDiff
}
