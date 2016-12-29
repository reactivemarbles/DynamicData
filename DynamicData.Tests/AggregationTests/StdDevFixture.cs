using System;
using System.Diagnostics;
using System.Linq;
using DynamicData.Aggregation;
using NUnit.Framework;

namespace DynamicData.Tests.AggregationTests
{
    [TestFixture]
    public class StdDevFixture
    {
        //TODO: TEST ACURACY

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [Explicit]
        public void CachePerformance(int n)
        {
            /*
                Tricks to make it fast = 

                1) use cache.AddRange(Enumerable.Range(0, n))
                instead of  for (int i = 0; i < n; i++) cache.AddOrUpdate(i);

                2)  Uncomment Buffer(n/10).FlattenBufferResult()
                or just use buffer by time functions

                With both of these the speed can be almost negligable

            */
            var cache = new SourceCache<int, int>(i => i);
            double calculated = 0;

            var sw = Stopwatch.StartNew();

            var summation = cache.Connect()
                                 .StdDev(i => i)
                                 .Subscribe(result => calculated = result);

            //1. this is very slow if there are loads of updates (each updates causes a new summation)
            for (int i = 1; i < n; i++)
                cache.AddOrUpdate(i);

            //2. much faster to to this (whole range is 1 update and 1 calculation):
            //  cache.AddOrUpdate(Enumerable.Range(0,n));

            sw.Stop();

            summation.Dispose();
            cache.Dispose();

            Console.WriteLine("Total items: {0}. Value = {1}", n, calculated);
            Console.WriteLine("Cache: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [Explicit]
        public void ListPerformance(int n)
        {
            var list = new SourceList<int>();
            int calculated = 0;

            var sw = Stopwatch.StartNew();

            var summation = list.Connect()
                                .Sum(i => i)
                                .Subscribe(result => calculated = result);

            //1. this is very slow if there are loads of updates (each updates causes a new summation)


            //2. very fast doing this (whole range is 1 update and 1 calculation):
            list.AddRange(Enumerable.Range(0, n));
            sw.Stop();

            summation.Dispose();
            list.Dispose();

            Console.WriteLine("Total items: {0}. Value = {1}", n, calculated);
            Console.WriteLine("List: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());
        }
    }
}
