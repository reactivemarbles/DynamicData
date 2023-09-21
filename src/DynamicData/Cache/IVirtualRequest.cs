// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// A request to virtualise a stream.
/// </summary>
public interface IVirtualRequest
{
    /// <summary>
    /// Gets the number of records to return.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the start index.
    /// </summary>
    int StartIndex { get; }
}
