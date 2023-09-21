// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Represents a new page request.
/// </summary>
public interface IPageRequest
{
    /// <summary>
    /// Gets the page to move to.
    /// </summary>
    int Page { get; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    int Size { get; }
}
