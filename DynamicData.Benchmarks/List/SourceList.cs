using System.Linq;
using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List
{
    [CoreJob]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class SourceList
    {
        private  SourceList<string> _sourceList;
        private string[] _items;

        [Params(1, 100, 1_000, 10_000, 100_000)]
        public int N;


        [GlobalSetup]
        public void Setup()
        {
            _sourceList = new SourceList<string>();
        }

        [IterationSetup]
        public void SetupIteration()
        {
            _sourceList.Clear();
            _items = Enumerable.Range(1, N).Select(i => i.ToString()).ToArray();
        }

        [GlobalCleanup]
        public void Teardown()
        {
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

        [Benchmark]
        public void Insert() => _sourceList.InsertRange(_items,0);
    }
}