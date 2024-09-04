// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if !NETCOREAPP
namespace System.Collections.Generic;

internal static class ListEnsureCapacity
{
    public static void EnsureCapacity<T>(this List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
            list.Capacity = capacity;
    }
}
#endif
