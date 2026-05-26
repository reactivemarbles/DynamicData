// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using DynamicData.Binding;

namespace DynamicData.Benchmarks.Cache;

/// <summary>
/// Multi-threaded SourceCache contention benchmarks. Measures aggregate throughput
/// when N threads write concurrently with varying subscriber complexity.
/// Exercises the DeliveryQueue path (ObservableCache).
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ContentionBenchmarks
{
    private SourceCache<ContentionItem, int> _cache = null!;
    private IDisposable? _subscription;

    [Params(1, 2, 4)]
    public int ThreadCount;

    [Params("None", "Sort", "Chain")]
    public string SubscriberWork = "None";

    private const int ItemsPerThread = 1_000;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cache = new SourceCache<ContentionItem, int>(i => i.Id);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _subscription?.Dispose();
        _cache.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _subscription?.Dispose();
        _cache.Clear();

        _subscription = SubscriberWork switch
        {
            "Sort" => _cache.Connect()
                .Sort(SortExpressionComparer<ContentionItem>.Ascending(i => i.Name))
                .Subscribe(_ => { }),

            "Chain" => _cache.Connect()
                .Filter(i => i.Price > 0)
                .Sort(SortExpressionComparer<ContentionItem>.Ascending(i => i.Name))
                .Transform(i => new ContentionItemVm(i))
                .Subscribe(_ => { }),

            _ => _cache.Connect().Subscribe(_ => { }),
        };
    }

    [Benchmark]
    public void ConcurrentAddOrUpdate()
    {
        if (ThreadCount == 1)
        {
            for (var i = 0; i < ItemsPerThread; i++)
                _cache.AddOrUpdate(new ContentionItem(i, $"Item_{i}", i * 0.1m));
        }
        else
        {
            var barrier = new Barrier(ThreadCount);
            var tasks = new Task[ThreadCount];

            for (var t = 0; t < ThreadCount; t++)
            {
                var threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < ItemsPerThread; i++)
                    {
                        var id = (threadId * ItemsPerThread) + i;
                        _cache.AddOrUpdate(new ContentionItem(id, $"Item_{id}", id * 0.1m));
                    }
                });
            }

            Task.WaitAll(tasks);
            barrier.Dispose();
        }
    }

    public sealed record ContentionItem(int Id, string Name, decimal Price);
    public sealed record ContentionItemVm(ContentionItem Source);
}

/// <summary>
/// MergeManyChangeSets contention benchmark. Multiple threads mutating child
/// SourceCaches while a CPS pipeline is subscribed. Uses SourceCache (not
/// SourceList) for children so the full path exercises DeliveryQueue + SDQ.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class MmcsContentionBenchmarks
{
    private SourceCache<MmcsParent, int> _parents = null!;
    private MmcsParent[] _parentItems = null!;
    private IDisposable? _subscription;

    [Params(1, 2, 4)]
    public int ThreadCount;

    [Params("None", "Sort", "Transform")]
    public string SubscriberWork = "None";

    private const int ParentCount = 50;
    private const int ChildOpsPerThread = 200;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _parents = new SourceCache<MmcsParent, int>(p => p.Id);
        _parentItems = Enumerable.Range(0, ParentCount).Select(i =>
        {
            var p = new MmcsParent(i);
            p.Children.Edit(u =>
            {
                for (var j = 0; j < 10; j++)
                    u.AddOrUpdate(new MmcsChild(i * 100 + j, $"Child_{i}_{j}"));
            });
            return p;
        }).ToArray();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _subscription?.Dispose();
        foreach (var p in _parentItems) p.Dispose();
        _parents.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _subscription?.Dispose();
        _parents.Clear();
        _parents.AddOrUpdate(_parentItems);

        var pipeline = _parents.Connect()
            .MergeManyChangeSets(p => p.Children.Connect());

        _subscription = SubscriberWork switch
        {
            "Sort" => pipeline
                .Sort(SortExpressionComparer<MmcsChild>.Ascending(c => c.Name))
                .Bind(out _)
                .Subscribe(_ => { }),

            "Transform" => pipeline
                .Transform(c => new MmcsChildVm(c))
                .Subscribe(_ => { }),

            _ => pipeline.Subscribe(_ => { }),
        };
    }

    [Benchmark]
    public void ConcurrentChildMutations()
    {
        if (ThreadCount == 1)
        {
            MutateChildren(0);
        }
        else
        {
            var barrier = new Barrier(ThreadCount);
            var tasks = new Task[ThreadCount];
            for (var t = 0; t < ThreadCount; t++)
            {
                var threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    MutateChildren(threadId);
                });
            }
            Task.WaitAll(tasks);
            barrier.Dispose();
        }
    }

    private void MutateChildren(int threadId)
    {
        for (var i = 0; i < ChildOpsPerThread; i++)
        {
            var parentIdx = (threadId * ChildOpsPerThread + i) % ParentCount;
            var parent = _parentItems[parentIdx];
            var childId = threadId * 100_000 + i;
            parent.Children.AddOrUpdate(new MmcsChild(childId, $"New_{childId}"));
            if (parent.Children.Count > 15)
                parent.Children.RemoveKey(parent.Children.Keys.First());
        }
    }

    public sealed record MmcsChild(int Id, string Name);
    public sealed record MmcsChildVm(MmcsChild Source);

    public sealed class MmcsParent(int id) : IDisposable
    {
        public int Id { get; } = id;
        public SourceCache<MmcsChild, int> Children { get; } = new(c => c.Id);
        public void Dispose() => Children.Dispose();
    }
}
