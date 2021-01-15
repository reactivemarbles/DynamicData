// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace DynamicData.Profile
{
    public static class Allocations
    {
        public static AllocationsCount Run(Action action)
        {
            var initial = GC.GetAllocatedBytesForCurrentThread();
            action();
            var final = GC.GetAllocatedBytesForCurrentThread();

            return new AllocationsCount(initial, final);
        }
    }
}