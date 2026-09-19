// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NETCOREAPP
namespace System.Collections.Generic;

/// <summary>
/// Provides members for the ListEnsureCapacity class.
/// </summary>
internal static class ListEnsureCapacity
{
        /// <summary>
    /// Executes the EnsureCapacity operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="list">The list value.</param>
    /// <param name="capacity">The capacity value.</param>
public static void EnsureCapacity<T>(this List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
            list.Capacity = capacity;
    }
}
#endif
