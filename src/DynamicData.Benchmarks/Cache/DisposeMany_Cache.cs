// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache
{
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class DisposeMany_Cache
    {
        [Benchmark]
        [Arguments(1, 0)]
        [Arguments(1, 1)]
        [Arguments(10, 0)]
        [Arguments(10, 1)]
        [Arguments(10, 10)]
        [Arguments(100, 0)]
        [Arguments(100, 1)]
        [Arguments(100, 10)]
        [Arguments(100, 100)]
        [Arguments(1_000, 0)]
        [Arguments(1_000, 1)]
        [Arguments(1_000, 10)]
        [Arguments(1_000, 100)]
        [Arguments(1_000, 1_000)]
        public void AddsRemovesAndFinalization(int addCount, int removeCount)
        {
            using var source = new SourceCache<KeyedDisposable, int>(static item => item.Id);

            using var subscription = source
                .Connect()
                .DisposeMany()
                .Subscribe();

            for(var i = 0; i < addCount; ++i)
                source.AddOrUpdate(new KeyedDisposable(i));

            for(var i = 0; i < removeCount; ++i)
                source.RemoveKey(i);

            subscription.Dispose();
        }

        private sealed class KeyedDisposable
            : IDisposable
        {
            public KeyedDisposable(int id)
                => Id = id;

            public int Id { get; }

            public void Dispose() { }
        }
    }
}
