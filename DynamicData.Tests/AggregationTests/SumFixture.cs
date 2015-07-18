using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Aggregation;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.AggregationTests
{
    [TestFixture]
    public class SumFixture
    {
        private SourceCache<Person, string> _source;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void AddedItemsContributeToSum()
        {
            int sum = 0;

            var accumulator = _source.Connect()
                .Sum(p => p.Age)
                .Subscribe(x => sum = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            Assert.AreEqual(60, sum, "Accumulated value should be 60");

            accumulator.Dispose();
        }

        [Test]
        public void RemoveProduceCorrectResult()
        {
            int sum = 0;

            var accumulator = _source.Connect()
                .Sum(p => p.Age)
                .Subscribe(x => sum = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            _source.Remove("A");
            Assert.AreEqual(50, sum, "Accumulated value should be 50 after remove");


            accumulator.Dispose();
        }

 
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]

        public void Simple(int n)
        {
            var list = new SourceList<int>();

            var list2 = new SourceCache<int,int>(i=>i);

            int total = 0;

          // var people = Enumerable.Range(1,n)

            var summation = list.Connect()

                .ForAggregate()
                .Sum(i => i)
                .Subscribe(result => total=result);

            var sw = Stopwatch.StartNew();

            //for (int i = 0; i < n; i++)
            //{
            //    list.Add(i);
            //}
           list.AddRange(Enumerable.Range(0, n));
          //  list2.AddOrUpdate(Enumerable.Range(0,n));
            sw.Stop();

            summation.Dispose();

            // 10000 updates took 1172 ms 0,117 ms each
            // 10000 updates took 10 ms 0,001 ms each
            Console.WriteLine("Total increments={0}. Sum = {1}",n , total);
            Console.WriteLine("// Simple: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());
        }

    }
}