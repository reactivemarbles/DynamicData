// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DynamicData.Benchmarks.List
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class GroupAdd
    {
        private IDisposable? _groupSubscription;
        private SourceList<int>? _sourceList;
        private int[] _items = Enumerable.Range(1, 100).ToArray();

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
            _sourceList?.Clear();
            _items = Enumerable.Range(1, N).ToArray();
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _groupSubscription?.Dispose();
            _sourceList?.Dispose();
            _sourceList = null;
        }

        //[Benchmark]
        //public void Add()
        //{
        //    foreach (var item in _items)
        //        _sourceList.Add(item);
        //}

        [Benchmark]
        public void AddRange() => _sourceList?.AddRange(_items);
    }
}
