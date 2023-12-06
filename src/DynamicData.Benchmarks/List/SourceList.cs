// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DynamicData.Benchmarks.List
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class SourceList
    {
        private SourceList<string>? _sourceList;
        private string[]? _items;

        [Params(1, 100, 1_000, 10_000, 100_000)]
        public int N;

        [GlobalSetup]
        public void Setup() => _sourceList = new SourceList<string>();

        [IterationSetup]
        public void SetupIteration()
        {
            _sourceList?.Clear();
            _items = Enumerable.Range(1, N).Select(i => i.ToString()).ToArray();
        }

        [GlobalCleanup]
        public void Teardown() => _sourceList = null;

        [Benchmark]
        public void AddRange() => _sourceList?.AddRange(_items!);

        [Benchmark]
        public void Insert() => _sourceList?.InsertRange(_items!, 0);
    }
}