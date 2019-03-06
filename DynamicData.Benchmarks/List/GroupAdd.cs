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

        private static readonly int[] _items = Enumerable.Range(1,100).ToArray();

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
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _groupSubscription.Dispose();
            _sourceList.Dispose();
            _sourceList = null;
        }

        [Benchmark]
        public void Add() => _sourceList.Add(_items[0]);

        [Benchmark]
        public void AddRange() => _sourceList.AddRange(_items);
    }
}
