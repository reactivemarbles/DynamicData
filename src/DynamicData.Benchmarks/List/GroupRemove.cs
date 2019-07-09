using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List
{
    [CoreJob]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class GroupRemove
    {
        private IDisposable _groupSubscription;
        private SourceList<int> _sourceList;

        private static readonly int[] _items = Enumerable.Range(1, 100).ToArray();

        [GlobalSetup]
        public void Setup()
        {
            _sourceList = new SourceList<int>();     
            _groupSubscription = _sourceList.Connect().GroupOn(i => i % 10).Subscribe();
        }

        [IterationSetup]
        public void SetupIteration()
        {
            _sourceList.AddRange(_items);
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _groupSubscription.Dispose();
            _sourceList.Dispose();
            _sourceList = null;
        }

        [Benchmark]
        public void RemoveAt() => _sourceList.RemoveAt(1);

        [Benchmark]
        public void Remove() => _sourceList.RemoveAt(_items[0]);

        [Benchmark]
        public void RemoveRange() => _sourceList.RemoveRange(40,20);

        [Benchmark]
        public void Clear() => _sourceList.Clear();
    }
}