// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Operators;

namespace DynamicData;

/// <summary>
/// Parameters associated with the page operation.
/// </summary>
/// <typeparam name="TObject"> The type of object.</typeparam>
/// <param name="Response"> Response parameters.</param>
/// <param name="Comparer"> The comparer used to order the items.</param>
/// <param name="Options"> The options used to perform virtualization.</param>
public record PageContext<TObject>(
    IPageResponse Response,
    IComparer<TObject> Comparer,
    SortAndPageOptions Options)
{
    internal static readonly PageContext<TObject> Empty = new
    (
        new PageResponse(0, 0, 0, 0),
        Comparer<TObject>.Default,
        new SortAndPageOptions()
    );
}
