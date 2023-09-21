// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace DynamicData;

/// <summary>
/// <para> The reason for an individual change.</para>
/// <para>Used to signal consumers of any changes to the underlying cache.</para>
/// </summary>
public enum ChangeReason
{
    /// <summary>
    ///  An item has been added.
    /// </summary>
    Add = 0,

    /// <summary>
    ///  An item has been updated.
    /// </summary>
    Update = 1,

    /// <summary>
    ///  An item has removed.
    /// </summary>
    Remove = 2,

    /// <summary>
    /// Downstream operators will refresh.
    /// </summary>
    Refresh = 3,

    /// <summary>
    /// An item has been moved in a sorted collection.
    /// </summary>
    Moved = 4,
}
