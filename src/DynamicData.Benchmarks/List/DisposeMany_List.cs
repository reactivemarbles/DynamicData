// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List
{
    [MemoryDiagnoser]
    [MarkdownExporterAttribute.GitHub]
    public class DisposeMany_List
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
            using var source = new SourceList<IDisposable>();

            using var subscription = source
                .Connect()
                .DisposeMany()
                .Subscribe();

            for(var i = 0; i < addCount; ++i)
                source.Add(Disposable.Create(static () => { }));

            while(source.Count > (addCount - removeCount))
                source.RemoveAt(source.Count - 1);

            subscription.Dispose();
        }
    }
}
