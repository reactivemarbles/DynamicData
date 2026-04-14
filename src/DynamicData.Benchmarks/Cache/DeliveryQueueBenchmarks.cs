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
/// Benchmarks measuring throughput impact of the queue-drain delivery
/// pattern. Covers single-threaded overhead with varying pipeline depth.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class DeliveryQueueBenchmarks
{
    private SourceCache<Item, int> _cache = null!;
    private Item[] _items = null!;
    private IDisposable? _subscription;

    [Params(100, 1_000, 10_000)]
    public int N;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cache = new SourceCache<Item, int>(i => i.Id);
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
        _items = Enumerable.Range(0, N).Select(i => new Item(i, $"Name_{i}", i * 0.5m)).ToArray();
    }

    [Benchmark(Baseline = true)]
    public void AddItems_NoSubscriber()
    {
        _cache.AddOrUpdate(_items);
    }

    [Benchmark]
    public void AddItems_WithSubscriber()
    {
        var count = 0;
        using var sub = _cache.Connect().Subscribe(_ => Interlocked.Increment(ref count));
        _cache.AddOrUpdate(_items);
    }

    [Benchmark]
    public void AddItems_SortPipeline()
    {
        using var sub = _cache.Connect()
            .Sort(SortExpressionComparer<Item>.Ascending(i => i.Name))
            .Subscribe(_ => { });
        _cache.AddOrUpdate(_items);
    }

    [Benchmark]
    public void AddItems_ChainedPipeline()
    {
        using var sub = _cache.Connect()
            .Filter(i => i.Price > 0)
            .Sort(SortExpressionComparer<Item>.Ascending(i => i.Name))
            .Transform(i => new ItemViewModel(i))
            .Subscribe(_ => { });
        _cache.AddOrUpdate(_items);
    }

    [Benchmark]
    public void AddItems_MergeManyChangeSets()
    {
        var parents = new SourceCache<Parent, int>(p => p.Id);
        using var sub = parents.Connect()
            .MergeManyChangeSets(p => p.Children.Connect())
            .Subscribe(_ => { });

        var parentItems = Enumerable.Range(0, Math.Max(1, N / 10)).Select(i =>
        {
            var p = new Parent(i);
            for (var j = 0; j < 10; j++)
                p.Children.Add(new Item(i * 10 + j, $"Child_{i}_{j}", j * 1.0m));
            return p;
        }).ToArray();

        parents.AddOrUpdate(parentItems);
        parents.Dispose();
    }

    public sealed record Item(int Id, string Name, decimal Price);
    public sealed record ItemViewModel(Item Source);

    public sealed class Parent(int id) : IDisposable
    {
        public int Id { get; } = id;
        public SourceList<Item> Children { get; } = new();
        public void Dispose() => Children.Dispose();
    }
}

/// <summary>
/// Multi-threaded contention benchmarks. Measures aggregate throughput
/// when N threads write concurrently with varying subscriber complexity.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ContentionBenchmarks
{
    private SourceCache<ContentionItem, int> _cache = null!;
    private IDisposable? _subscription;

    [Params(1, 2, 4, 8)]
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
/// MergeManyChangeSets contention benchmark. Multiple threads mutating
/// child SourceLists while a CPS pipeline is subscribed.
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

    private const int ParentCount = 50;
    private const int ChildOpsPerThread = 200;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _parents = new SourceCache<MmcsParent, int>(p => p.Id);
        _parentItems = Enumerable.Range(0, ParentCount).Select(i =>
        {
            var p = new MmcsParent(i);
            for (var j = 0; j < 10; j++)
                p.Children.Add(new MmcsChild(i * 100 + j, $"Child_{i}_{j}"));
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

        _subscription = _parents.Connect()
            .MergeManyChangeSets(p => p.Children.Connect())
            .Subscribe(_ => { });
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
            parent.Children.Add(new MmcsChild(childId, $"New_{childId}"));
            if (parent.Children.Count > 15)
                parent.Children.RemoveAt(0);
        }
    }

    public sealed record MmcsChild(int Id, string Name);

    public sealed class MmcsParent(int id) : IDisposable
    {
        public int Id { get; } = id;
        public SourceList<MmcsChild> Children { get; } = new();
        public void Dispose() => Children.Dispose();
    }
}
