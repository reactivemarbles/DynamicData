// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Parameters associated with the virtualize operation.
/// </summary>
/// <typeparam name="TObject"> The type of object.</typeparam>
/// <param name="Response"> Response parameters.</param>
/// <param name="Comparer"> The comparer used to order the items.</param>
/// <param name="Options"> The options used to perform virtualization.</param>
public record VirtualContext<TObject>(
    IVirtualResponse Response,
    IComparer<TObject> Comparer,
    SortAndVirtualizeOptions Options)
{
    internal static readonly VirtualContext<TObject> Empty = new
    (
        new VirtualResponse(0, 0, 0),
        Comparer<TObject>.Default,
        new SortAndVirtualizeOptions()
    );
}
