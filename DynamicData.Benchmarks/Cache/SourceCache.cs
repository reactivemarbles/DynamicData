using System.Linq;
using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache
{
    public class BenchmarkItem
    {
        public int Id { get; }

        public BenchmarkItem(int id)
        {
            Id = id;
        }
    }


    [CoreJob]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class SourceCache
    {
        private SourceCache<BenchmarkItem, int> _cache;
        private  BenchmarkItem[] _items = Enumerable.Range(1,100).Select(i=> new BenchmarkItem(i)).ToArray();

        [GlobalSetup]
        public void Setup()
        {
            _cache = new SourceCache<BenchmarkItem, int>(i=> i.Id);
        }

        [Params(1, 100, 1_000, 10_000, 100_000)]
        public int N;

        [IterationSetup]
        public void SetupIteration()
        {
            _cache.Clear();
            _items = Enumerable.Range(1, N).Select(i => new BenchmarkItem(i)).ToArray();
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _cache.Dispose();
            _cache = null;
        }

        [Benchmark]
        public void Add() => _cache.AddOrUpdate(_items);
    }
}
