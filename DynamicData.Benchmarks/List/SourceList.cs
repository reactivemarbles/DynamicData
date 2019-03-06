using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List
{
    [CoreJob]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class SourceList
    {
        private  SourceList<string> _sourceList;
        private static readonly string[] _items =  new[]
        {
            "Item1",
            "Item2",
            "Item3",
            "Item4",
            "Item5",
            "Item6",
            "Item7",
            "Item8",
            "Item9",
            "Item10"
        };

        [GlobalSetup]
        public void Setup()
        {
            _sourceList = new SourceList<string>();
        }

        [IterationSetup]
        public void SetupIteration()
        {
            _sourceList.Clear();
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _sourceList = null;
        }

        [Benchmark]
        public void Add() => _sourceList.Add(_items[0]);
        
        [Benchmark]
        public void AddRange() => _sourceList.AddRange(_items);

        [Benchmark]
        public void Insert() => _sourceList.Insert(0, _items[0]);

        [Benchmark]
        public void RemoveItem() => _sourceList.Remove(_items[0]);
    }
}