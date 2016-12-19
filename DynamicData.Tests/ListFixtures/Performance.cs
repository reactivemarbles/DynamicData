using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture()]
    public class Performance
    {
        [Test]
        [Explicit]
        public void Test()
        {
            for (int i = 1; i < 8; i++)
            {
                var amount = (int)Math.Pow(10, i);
                var items = Enumerable.Range(1, amount).Select(x => x).ToList();

                Timer.ToConsole(() => { WithRange(items); }, 1, $"With Range for {amount}");

                Timer.ToConsole(() => { WithCapacity(items); }, 1, $"With capacity for {amount}");

                Timer.ToConsole(() => { WithoutCapacity(items); }, 1, $"Without capacity for {amount}");

                Timer.ToConsole(() => { Dictionary(items); }, 1, $"Dictionary for {amount}");

                Timer.ToConsole(() => { HashSet(items); }, 1, $"HashSet for {amount}");

                Console.WriteLine();
            }
        }

        [Test]
        [Explicit]
        public void TestWithOutCapacity()
        {
            for (int i = 1; i < 6; i++)
            {
                var amount = (int)Math.Pow(10, i);
                var items = Enumerable.Range(1, amount).Select(x => x).ToList();

                Timer.ToConsole(() => { WithoutCapacity(items); }, 10, $"With capacity for {amount}");
            }
        }

        private void WithCapacity(List<int> items)
        {
            var list = new List<int>();
            list.Capacity = items.Count;

            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        private void WithoutCapacity(List<int> items)
        {
            var list = new List<int>();
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        private void WithRange(List<int> items)
        {
            var list = new List<int>();
            list.AddRange(items);
        }

        private void HashSet(List<int> items)
        {
            var list = new HashSet<int>();
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        private void Dictionary(List<int> items)
        {
            var list = new Dictionary<int, int>();
            foreach (var item in items)
            {
                list[item] = item;
            }
        }
    }
}
