using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List
{
    [CoreJob]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class GroupAdd
    {
        private IDisposable _groupSubscription;
        private SourceList<int> _sourceList;
        private int[] _items = Enumerable.Range(1,100).ToArray();

        [Params(1, 100, 1_000, 10_000, 100_000)]
        public int N;

        [GlobalSetup]
        public void Setup()
        {
            _sourceList = new SourceList<int>();
            _groupSubscription = _sourceList.Connect().GroupOn(i => i % 10).Subscribe();
        }

        [IterationSetup]
        public void SetupIteration()
        {
            _sourceList.Clear();
            _items = Enumerable.Range(1, N).ToArray();
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _groupSubscription.Dispose();
            _sourceList.Dispose();
            _sourceList = null;
        }

        //[Benchmark]
        //public void Add()
        //{
        //    foreach (var item in _items)
        //        _sourceList.Add(item);
        //}

        [Benchmark]
        public void AddRange() => _sourceList.AddRange(_items);
    }
}
