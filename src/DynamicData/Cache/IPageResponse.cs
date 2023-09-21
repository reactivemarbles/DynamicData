// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Operators;

/// <summary>
/// Response from the pagination operator.
/// </summary>
public interface IPageResponse
{
    /// <summary>
    /// Gets the current page.
    /// </summary>
    int Page { get; }

    /// <summary>
    /// Gets total number of pages.
    /// </summary>
    int Pages { get; }

    /// <summary>
    /// Gets the size of the page.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Gets the total number of records in the underlying cache.
    /// </summary>
    int TotalSize { get; }
}
