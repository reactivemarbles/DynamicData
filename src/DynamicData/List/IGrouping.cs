// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List;

/// <summary>
/// Represents a group which provides an update after any value within the group changes.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TGroupKey">The type of the group key.</typeparam>
public interface IGrouping<out TObject, out TGroupKey>
{
    /// <summary>
    /// Gets the count.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    IEnumerable<TObject> Items { get; }

    /// <summary>
    /// Gets the group key.
    /// </summary>
    TGroupKey Key { get; }
}
