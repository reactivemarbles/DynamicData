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
    public class GroupRemove
    {
        private IDisposable? _groupSubscription;
        private SourceList<int>? _sourceList;

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
            _sourceList?.AddRange(_items);
        }

        [GlobalCleanup]
        public void Teardown()
        {
            _groupSubscription?.Dispose();
            _sourceList?.Dispose();
            _sourceList = null;
        }

        [Benchmark]
        public void RemoveAt() => _sourceList?.RemoveAt(1);

        [Benchmark]
        public void Remove() => _sourceList?.RemoveAt(_items[0]);

        [Benchmark]
        public void RemoveRange() => _sourceList?.RemoveRange(40, 20);

        [Benchmark]
        public void Clear() => _sourceList?.Clear();
    }
}