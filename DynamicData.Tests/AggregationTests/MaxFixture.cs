using System;
using System.Diagnostics;
using System.Linq;
using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.AggregationTests
{
    [TestFixture]
    public class MaxFixture
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
        public void AddItems()
        {
            var result = 0;

            var accumulator = _source.Connect()
                                     .Maximum(p => p.Age)
                                     .Subscribe(x => result = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            result.Should().Be(30, "Max value should be 30");

            accumulator.Dispose();
        }

        [Test]
        public void RemoveItems()
        {
            var result = 0;

            var accumulator = _source.Connect()
                                     .Maximum(p => p.Age)
                                     .Subscribe(x => result = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            _source.Remove("C");
            result.Should().Be(20, "Max value should be 20 after remove");
            accumulator.Dispose();
        }

        [Test]
        public void InlineChangeReEvaluatesTotals()
        {
            double max = 0;

            var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

            var accumulator = _source.Connect()
                .Maximum(p => p.Age)
                .InvalidateWhen(somepropChanged)
                .Subscribe(x => max = x);

            var personc = new Person("C", 5);
            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 11));
            _source.AddOrUpdate(personc);

            max.Should().Be(11, "Max should be 11");

            personc.Age = 100;

            max.Should().Be(100, "Max should be 100 after inline change");
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
                                 .Maximum(i => i)
                                 .Subscribe(result => runningSum = result);

            //1. this is very slow if there are loads of updates (each updates causes a new summation)
            //for (int i = 1; i < n; i++)
            //    cache.AddOrUpdate(i);

            //2. much faster to to this (whole range is 1 update and 1 calculation):
            cache.AddOrUpdate(Enumerable.Range(0, n));

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
            int result = 0;

            var sw = Stopwatch.StartNew();
            var list = new SourceList<int>();

            var summation = list.Connect()
                                .Maximum(i => i)
                                .Subscribe(x => result = x);

            //1. this is very slow if there are loads of updates (each updates causes a new summation)
            for (int i = 0; i < n; i++)
                list.Add(i);

            //2. very fast doing this (whole range is 1 update and 1 calculation):
            //list.AddRange(Enumerable.Range(0, n));
            sw.Stop();

            summation.Dispose();
            list.Dispose();

            Console.WriteLine("Total items: {0}. Sum = {1}", n, result);
            Console.WriteLine("List: {0} updates took {1} ms {2:F3} ms each. {3}", n, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / n, DateTime.Now.ToShortDateString());
        }
    }
}
