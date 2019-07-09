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