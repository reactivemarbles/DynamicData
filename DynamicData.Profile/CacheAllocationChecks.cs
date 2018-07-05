using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace DynamicData.Profile
{

    public class CacheAllocationChecks
    {
        [Fact]
        public void CheckAllocations()
        {
            var items = Enumerable.Range(1, 10_000).Select(j => new Person("P" + j, j)).ToList();

            var cache = new SourceCache<Person, string>(p=>p.Name);

            var result = Allocations.Run(() =>
            {
                cache.AddOrUpdate(items);
            });
            Console.WriteLine(result);
        }
    }


    public class AllocationsCount
    {

        public long InitialSize { get; }
        public long FinalSize { get; }
        public long Size => FinalSize - InitialSize;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public AllocationsCount(long initialSize, long finalSize)
        {
            InitialSize = initialSize;
            FinalSize = finalSize;
        }

        public override string ToString()
        {
            return $"Allocation Bytes: {Size}";
        }
    }

}
