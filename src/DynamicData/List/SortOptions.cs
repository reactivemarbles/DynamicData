// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace DynamicData;

/// <summary>
/// Options for sorting.
/// </summary>
[SuppressMessage("Design", "CA1717: Only flags should have plural names", Justification = "Backwards compatibility")]
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
