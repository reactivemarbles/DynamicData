// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Defines values used to virtualise the result set.
/// </summary>
public interface IVirtualResponse
{
    /// <summary>
    /// Gets the requested size of the virtualised data.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the start index.
    /// </summary>
    int StartIndex { get; }

    /// <summary>
    /// Gets the total size of the underlying cache.
    /// </summary>
    /// <value>
    /// The total size.
    /// </value>
    int TotalSize { get; }
}
