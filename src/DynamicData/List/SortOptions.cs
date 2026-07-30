// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Options for sorting.
/// </summary>
public enum SortOptions
{
    /// <summary>
    /// No sort options are specified.
    /// </summary>
    None,

    /// <summary>
    /// Use binary search to locate item index.
    /// </summary>
    UseBinarySearch
}
