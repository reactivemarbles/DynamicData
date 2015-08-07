using System;
using System.Diagnostics;
using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.AggregationTests
{


    [TestFixture]
    public class MinFixture
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
            var result = 0;

            var accumulator = _source.Connect()
                .Minimum(p => p.Age)
                .Subscribe(x => result = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            Assert.AreEqual(10, result, "Min value should be 10");

            accumulator.Dispose();
        }

        [Test]
        public void RemoveProduceCorrectResult()
        {
            var result = 0;

            var accumulator = _source.Connect()
                .Minimum(p => p.Age)
                .Subscribe(x => result = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            _source.Remove("A");
            Assert.AreEqual(20, result, "Min value should be 20 after remove");
            accumulator.Dispose();
        }

        [Test]
        public void InlineChangeReEvaluatesTotals()
        {
            double min = 0;

            var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

            var accumulator = _source.Connect()
                .Minimum(p => p.Age)
                .InvalidateWhen(somepropChanged)
                .Subscribe(x => min = x);

            var personc = new Person("C", 5);
            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 11));
            _source.AddOrUpdate(personc);
            Assert.AreEqual(5, min, "Min should be 5");

            _source.AddOrUpdate(personc);




            personc.Age = 11;

            Assert.AreEqual(10, min, "Min should be 10 after inline change");
            accumulator.Dispose();
        }



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
            double runningSum = 0;

            var sw = Stopwatch.StartNew();

            var summation = cache.Connect()
                .Minimum(i => i)
                .Subscribe(result => runningSum = result);


            //1. this is very slow if there are loads of updates (each updates causes a new summation)
            for (int i = 1; i < n; i++)
                cache.AddOrUpdate(i);

            //2. much faster to to this (whole range is 1 update and 1 calculation):
            //  cache.AddOrUpdate(Enumerable.Range(0,n));

            sw.Stop();

            summation.Dispose();
            cache.Dispose();

            Console.WriteLine("Total items: {0}. Sum = {1}", n, runningSum);
            Console.WriteLine("Cache Summation: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());

        }


        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [Explicit]
        public void ListPerformance(int n)
        {
            var list = new SourceList<int>();
            double runningSum = 0;

            var sw = Stopwatch.StartNew();

            var summation = list.Connect()
                .Minimum(i => i)
                .Subscribe(result => runningSum = result);


            //1. this is very slow if there are loads of updates (each updates causes a new summation)
            for (int i = 0; i < n; i++)
                list.Add(i);

            //2. very fast doing this (whole range is 1 update and 1 calculation):
            //list.AddRange(Enumerable.Range(0, n));
            sw.Stop();

            summation.Dispose();
            list.Dispose();

            Console.WriteLine("Total items: {0}. Sum = {1}", n, runningSum);
            Console.WriteLine("List: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());

        }

    }
}