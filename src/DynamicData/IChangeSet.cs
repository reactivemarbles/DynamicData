// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Base interface representing a set of changes.
/// </summary>
public interface IChangeSet
{
    /// <summary>
    ///     Gets the number of additions.
    /// </summary>
    int Adds { get; }

    /// <summary>
    /// Gets or sets the capacity of the change set.
    /// </summary>
    /// <value>
    /// The capacity.
    /// </value>
    int Capacity { get; set; }

    /// <summary>
    ///     Gets the total update count.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets the number of moves.
    /// </summary>
    int Moves { get; }

    /// <summary>
    /// Gets the number of refreshes.
    /// </summary>
    int Refreshes { get; }

    /// <summary>
    ///     Gets the number of removes.
    /// </summary>
    int Removes { get; }
}
