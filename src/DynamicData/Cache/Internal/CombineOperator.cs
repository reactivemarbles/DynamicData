// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// How the multiple streams are combinedL.
/// </summary>
public enum CombineOperator
{
    /// <summary>
    /// Apply a logical And between two or more observable change sets.
    /// </summary>
    And,

    /// <summary>
    /// Apply a logical Or between two or more observable change sets.
    /// </summary>
    Or,

    /// <summary>
    /// Apply a logical Xor between two or more observable change sets.
    /// </summary>
    Xor,

    /// <summary>
    /// Include the items in the first change set and exclude any items belonging to the other.
    /// </summary>
    Except
}
