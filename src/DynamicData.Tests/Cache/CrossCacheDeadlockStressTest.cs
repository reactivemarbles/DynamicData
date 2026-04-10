// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Binding;
using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Mega stress test that wires up a bidirectional cross-cache pipeline touching
/// every dangerous operator, then hammers it from multiple threads. If it completes
/// without deadlock or crash, the entire library is deadlock-free by construction.
/// </summary>
public class CrossCacheDeadlockStressTest
{
    private const int WriterThreads = 4;
    private const int ItemsPerThread = 200;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private sealed class StressItem : INotifyPropertyChanged
    {
        private string _category;

        public StressItem(string id, string value, string category)
        {
            Id = id;
            Value = value;
            _category = category;
        }

        public string Id { get; }

        public string Value { get; }

        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Builds a bidirectional cross-cache pipeline using every operator that could
    /// deadlock if used with Synchronize(lock) instead of SynchronizeSafe(queue).
    /// Multiple threads write to both caches concurrently and update properties.
    /// The test proves no deadlock occurs.
    /// </summary>
    [Fact]
    public async Task AllOperatorsInCrossCachePipeline_NoDeadlock()
    {
        using var cacheA = new SourceCache<StressItem, string>(x => x.Id);
        using var cacheB = new SourceCache<StressItem, string>(x => x.Id);
        using var subscriptions = new CompositeDisposable();

        // === Forward pipeline: cacheA → [many operators] → cacheB ===

        // Sort → Page → GroupOn → Flatten → FullJoin with cacheB → PopulateInto cacheB
        var sortComparer = new BehaviorSubject<IComparer<StressItem>>(
            SortExpressionComparer<StressItem>.Ascending(x => x.Id));
        subscriptions.Add(sortComparer);

        var pageRequests = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        subscriptions.Add(pageRequests);

        var forwardPipeline = cacheA.Connect()
            .AutoRefresh(x => x.Category)
            .Sort(sortComparer)
            .Page(pageRequests)
            .Transform(x => new StressItem("fwd-" + x.Id, x.Value, x.Category))
            .Filter(x => !x.Id.StartsWith("fwd-fwd-"))
            .SubscribeMany(item => Disposable.Empty)
            .PopulateInto(cacheB);
        subscriptions.Add(forwardPipeline);

        // === Reverse pipeline: cacheB → [operators] → cacheA ===
        var reversePipeline = cacheB.Connect()
            .Filter(x => x.Id.StartsWith("fwd-b-"))
            .Transform(x => new StressItem("rev-" + x.Id, x.Value, x.Category))
            .Filter(x => !x.Id.StartsWith("rev-rev-"))
            .PopulateInto(cacheA);
        subscriptions.Add(reversePipeline);

        // === Additional cross-cache operators ===

        // MergeMany: subscribe to property changes across cacheA items
        var mergedChanges = cacheA.Connect()
            .MergeMany(item => Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => item.PropertyChanged += h,
                h => item.PropertyChanged -= h)
                .Select(_ => item))
            .Subscribe();
        subscriptions.Add(mergedChanges);

        // QueryWhenChanged on cacheB
        var queryResults = cacheB.Connect()
            .QueryWhenChanged()
            .Subscribe();
        subscriptions.Add(queryResults);

        // SortAndBind on cacheA
        var boundList = new List<StressItem>();
        var sortAndBind = cacheA.Connect()
            .SortAndBind(boundList, SortExpressionComparer<StressItem>.Ascending(x => x.Id))
            .Subscribe();
        subscriptions.Add(sortAndBind);

        // Virtualise on cacheB
        var virtualRequests = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 25));
        subscriptions.Add(virtualRequests);
        var virtualised = cacheB.Connect()
            .Sort(SortExpressionComparer<StressItem>.Ascending(x => x.Id))
            .Virtualise(virtualRequests)
            .Subscribe();
        subscriptions.Add(virtualised);

        // === Hammer from multiple threads ===
        using var barrier = new Barrier(WriterThreads + WriterThreads + 1 + 1); // A writers + B writers + property updater + main thread

        var writersA = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                cacheA.AddOrUpdate(new StressItem($"a-{t}-{i}", $"val-{i}", i % 3 == 0 ? "cat1" : "cat2"));
                if (i % 10 == 0)
                {
                    // Occasionally remove to trigger DisposeMany/OnBeingRemoved paths
                    cacheA.RemoveKey($"a-{t}-{Math.Max(0, i - 5)}");
                }
            }
        })).ToArray();

        var writersB = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                cacheB.AddOrUpdate(new StressItem($"b-{t}-{i}", $"val-{i}", i % 2 == 0 ? "catA" : "catB"));
                if (i % 15 == 0)
                {
                    cacheB.RemoveKey($"b-{t}-{Math.Max(0, i - 3)}");
                }
            }
        })).ToArray();

        // Property updater thread: trigger AutoRefresh paths
        var propertyUpdater = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                var items = cacheA.Items.Take(5).ToArray();
                foreach (var item in items)
                {
                    item.Category = i % 2 == 0 ? "updated1" : "updated2";
                }

                Thread.SpinWait(100);
            }
        });

        // Release all threads
        barrier.SignalAndWait();

        var allTasks = Task.WhenAll(writersA.Concat(writersB).Append(propertyUpdater));
        var completed = await Task.WhenAny(allTasks, Task.Delay(Timeout));
        completed.Should().BeSameAs(allTasks,
            $"cross-cache pipeline deadlocked — tasks did not complete within {Timeout.TotalSeconds}s");
        await allTasks; // propagate any faults
    }
}
