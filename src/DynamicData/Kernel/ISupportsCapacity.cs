// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// A collection type that supports a capacity.
/// </summary>
internal interface ISupportsCapacity
{
    /// <summary>
    /// Gets or sets the capacity.
    /// </summary>
    int Capacity { get; set; }

    /// <summary>
    /// Gets the number of items.
    /// </summary>
    int Count { get; }
}
