// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// <para>The reason for an individual change to an observable list.</para>
/// <para>Used to signal consumers of any changes to the underlying cache.</para>
/// </summary>
public enum ListChangeReason
{
    /// <summary>
    ///  An item has been added.
    /// </summary>
    Add,

    /// <summary>
    /// A range of items has been added.
    /// </summary>
    AddRange,

    /// <summary>
    ///  An item has been replaced.
    /// </summary>
    Replace,

    /// <summary>
    ///  An item has removed.
    /// </summary>
    Remove,

    /// <summary>
    /// A range of items has been removed.
    /// </summary>
    RemoveRange,

    /// <summary>
    ///   Command to operators to re-evaluate.
    /// </summary>
    Refresh,

    /// <summary>
    /// An item has been moved in a sorted collection.
    /// </summary>
    Moved,

    /// <summary>
    /// The entire collection has been cleared.
    /// </summary>
    Clear,
}
