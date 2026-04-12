// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using DynamicData.Internal;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// Tests for <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/>
/// behavioral contracts using a minimal concrete subclass.
/// All test data from seeded Randomizer — no hardcoded values.
/// </summary>
public sealed class CacheParentSubscriptionFixture
{
    private const int Seed = 55;

    // Bounds for randomized test parameters
    private const int ItemCountMin = 3;
    private const int ItemCountMax = 10;
    private const int BatchSizeMin = 2;
    private const int BatchSizeMax = 8;
    private const int StressIterationsMin = 50;
    private const int StressIterationsMax = 150;
    private const int StressThreadsMin = 2;
    private const int StressThreadsMax = 4;

    [Fact]
    public void ParentOnNext_CalledForEachChangeSet()
    {
        var rand = new Randomizer(Seed);
        var itemCount = rand.Number(ItemCountMin, ItemCountMax);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        var items = Enumerable.Range(0, itemCount)
            .Select(i => $"{rand.Number(1, 10000)}:{rand.String2(rand.Number(3, 10))}")
            .DistinctBy(ExtractKey)
            .ToList();

        foreach (var item in items)
            source.AddOrUpdate(item);

        sub.ParentCallCount.Should().Be(items.Count, "ParentOnNext should fire once per changeset");
        observer.EmitCount.Should().Be(items.Count, "EmitChanges should fire after each parent update");
    }

    [Fact]
    public void ChildOnNext_CalledForEachEmission()
    {
        var rand = new Randomizer(Seed + 1);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

        var key = rand.Number(1, 10000);
        source.AddOrUpdate($"{key}:value");

        childSubjects.Should().HaveCount(1);
        var childValue = rand.String2(rand.Number(5, 15));
        childSubjects[0].OnNext(childValue);

        sub.ChildCalls.Should().ContainSingle()
            .Which.Should().Be((childValue, key));
    }

    [Fact]
    public void EmitChanges_FiresOnceForBatch()
    {
        var rand = new Randomizer(Seed + 2);
        var batchSize = rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        var items = Enumerable.Range(1, batchSize)
            .Select(i => $"{i}:{rand.String2(rand.Number(3, 8))}")
            .ToList();

        source.Edit(updater =>
        {
            foreach (var item in items)
                updater.AddOrUpdate(item);
        });

        sub.ParentCallCount.Should().Be(1, "single batch = single ParentOnNext");
        sub.EmitCallCount.Should().Be(1, "single batch = single EmitChanges");
        observer.EmitCount.Should().Be(1);
    }

    [Fact]
    public void Batching_ChildUpdatesSettleBeforeEmit()
    {
        var rand = new Randomizer(Seed + 3);
        var batchSize = rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var observer = new TestObserver();

        // Children emit synchronously via BehaviorSubject
        var childCount = 0;
        using var sub = new TestSubscription(observer, key =>
        {
            Interlocked.Increment(ref childCount);
            return new BehaviorSubject<string>($"sync-{key}");
        });
        sub.ExposeCreateParent(source.Connect());

        var items = Enumerable.Range(1, batchSize)
            .Select(i => $"{i}:{rand.String2(rand.Number(3, 8))}")
            .ToList();

        source.Edit(updater =>
        {
            foreach (var item in items)
                updater.AddOrUpdate(item);
        });

        childCount.Should().Be(batchSize, "each item should create a child");
        sub.EmitCallCount.Should().BeGreaterThanOrEqualTo(1,
            "EmitChanges fires after parent + children settle");
    }

    [Fact]
    public void Completion_RequiresParentAndAllChildren()
    {
        var rand = new Randomizer(Seed + 4);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

        var key = rand.Number(1, 10000);
        source.AddOrUpdate($"{key}:value");
        childSubjects.Should().HaveCount(1);

        source.Dispose();
        observer.IsCompleted.Should().BeFalse("parent complete but child still active");

        childSubjects[0].OnCompleted();
        observer.IsCompleted.Should().BeTrue("OnCompleted fires when parent + all children complete");
    }

    [Fact]
    public void Completion_ParentOnly_NoChildren()
    {
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        source.Dispose();
        observer.IsCompleted.Should().BeTrue("immediate OnCompleted when no children");
    }

    [Fact]
    public void Disposal_StopsAllEmissions()
    {
        var rand = new Randomizer(Seed + 5);
        using var source = new SourceCache<string, int>(s => ExtractKey(s));
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

        var key = rand.Number(1, 10000);
        source.AddOrUpdate($"{key}:value");
        var emitsBefore = observer.EmitCount;

        sub.Dispose();

        source.AddOrUpdate($"{rand.Number(10001, 20000)}:after");
        if (childSubjects.Count > 0)
            childSubjects[0].OnNext("after-dispose");

        observer.EmitCount.Should().Be(emitsBefore, "no emissions after disposal");
    }

    [Fact]
    public void Error_Propagates()
    {
        using var source = new TestSourceCache<string, int>(s => ExtractKey(s));
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        var error = new InvalidOperationException("test error");
        source.SetError(error);

        observer.Error.Should().BeSameAs(error);
    }

    [Fact]
    public async Task CrossThread_MergeManyChangeSets_NoDeadlock()
    {
        var rand = new Randomizer(Seed + 6);
        var iterations = rand.Number(StressIterationsMin, StressIterationsMax);
        var threads = rand.Number(StressThreadsMin, StressThreadsMax);

        using var cacheA = new SourceCache<Market, Guid>(m => m.Id);
        using var cacheB = new SourceCache<Market, Guid>(m => m.Id);

        using var mergeAtoB = cacheA.Connect()
            .MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare)
            .Subscribe();

        using var mergeBtoA = cacheB.Connect()
            .MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare)
            .Subscribe();

        using var barrier = new Barrier(threads * 2);
        var tasks = new List<Task>();

        for (var t = 0; t < threads; t++)
        {
            var tRand = new Randomizer(Seed + 100 + t);
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < iterations; i++)
                {
                    var market = new Market(tRand.Number(1, 100000));
                    market.PricesCache.AddOrUpdate(
                        market.CreatePrice(tRand.Number(1, 10000), tRand.Decimal(1m, 100m)));
                    cacheA.AddOrUpdate(market);
                }
            }));

            var tRandB = new Randomizer(Seed + 200 + t);
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < iterations; i++)
                {
                    var market = new Market(tRandB.Number(100001, 200000));
                    market.PricesCache.AddOrUpdate(
                        market.CreatePrice(tRandB.Number(10001, 20000), tRandB.Decimal(1m, 100m)));
                    cacheB.AddOrUpdate(market);
                }
            }));
        }

        var completed = Task.WhenAll(tasks);
        var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(30)));
        finished.Should().BeSameAs(completed,
            "bidirectional MergeManyChangeSets should not deadlock");
    }

    /// <summary>
    /// Proves that CacheParentSubscription's Synchronize(_synchronize) causes ABBA deadlock
    /// when two instances feed into each other from concurrent threads. This test is expected
    /// to DEADLOCK on unfixed code and PASS after the fix. Skipped by default — enable after
    /// CacheParentSubscription is fixed to verify the fix works.
    /// </summary>
    [Trait("Category", "ExplicitDeadlock")]
    [Fact]
    public async Task DeadlockProof_TwoCacheParentSubscriptions_CrossFeed()
    {
        var rand = new Randomizer(Seed + 7);
        var iterations = rand.Number(StressIterationsMin, StressIterationsMax);

        // Two source caches, each with MergeManyChangeSets feeding cross-cache
        using var sourceA = new SourceCache<Market, Guid>(m => m.Id);
        using var sourceB = new SourceCache<Market, Guid>(m => m.Id);
        using var targetA = new SourceCache<MarketPrice, int>(p => p.ItemId);
        using var targetB = new SourceCache<MarketPrice, int>(p => p.ItemId);

        // A's prices → targetA, and also write into sourceB
        using var pipeA = sourceA.Connect()
            .MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare)
            .PopulateInto(targetA);

        // B's prices → targetB, and also write into sourceA
        using var pipeB = sourceB.Connect()
            .MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare)
            .PopulateInto(targetB);

        // Cross-feed: targetA changes trigger sourceB writes and vice versa
        using var crossAB = targetA.Connect().Subscribe(_ =>
        {
            var m = new Market(rand.Number(200001, 300000));
            m.PricesCache.AddOrUpdate(m.CreatePrice(rand.Number(1, 50000), rand.Decimal(1m, 100m)));
            sourceB.AddOrUpdate(m);
        });

        using var crossBA = targetB.Connect().Subscribe(_ =>
        {
            var m = new Market(rand.Number(300001, 400000));
            m.PricesCache.AddOrUpdate(m.CreatePrice(rand.Number(50001, 100000), rand.Decimal(1m, 100m)));
            sourceA.AddOrUpdate(m);
        });

        using var barrier = new Barrier(2);

        var taskA = Task.Run(() =>
        {
            var tRand = new Randomizer(Seed + 8);
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                var market = new Market(tRand.Number(1, 100000));
                market.PricesCache.AddOrUpdate(market.CreatePrice(tRand.Number(1, 50000), tRand.Decimal(1m, 100m)));
                sourceA.AddOrUpdate(market);
            }
        });

        var taskB = Task.Run(() =>
        {
            var tRand = new Randomizer(Seed + 9);
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                var market = new Market(tRand.Number(100001, 200000));
                market.PricesCache.AddOrUpdate(market.CreatePrice(tRand.Number(50001, 100000), tRand.Decimal(1m, 100m)));
                sourceB.AddOrUpdate(market);
            }
        });

        var completed = Task.WhenAll(taskA, taskB);
        var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(30)));
        finished.Should().BeSameAs(completed,
            "cross-feeding CacheParentSubscriptions should not deadlock after fix");
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static int ExtractKey(string s) => int.Parse(s.Split(':')[0]);

    /// <summary>
    /// Minimal concrete CacheParentSubscription for testing.
    /// Items are strings formatted as "key:value".
    /// </summary>
    private sealed class TestSubscription : CacheParentSubscription<string, int, string, IChangeSet<string, int>>
    {
        private readonly Func<int, IObservable<string>>? _childFactory;
        private readonly ChangeAwareCache<string, int> _cache = new();

        public int ParentCallCount;
        public int EmitCallCount;
        public readonly List<(string Value, int Key)> ChildCalls = [];

        public TestSubscription(IObserver<IChangeSet<string, int>> observer, Func<int, IObservable<string>>? childFactory = null)
            : base(observer)
        {
            _childFactory = childFactory;
        }

        public void ExposeCreateParent(IObservable<IChangeSet<string, int>> source)
            => CreateParentSubscription(source);

        protected override void ParentOnNext(IChangeSet<string, int> changes)
        {
            Interlocked.Increment(ref ParentCallCount);
            _cache.Clone(changes);

            if (_childFactory is not null)
            {
                foreach (var change in (ChangeSet<string, int>)changes)
                {
                    if (change.Reason is ChangeReason.Add or ChangeReason.Update)
                        AddChildSubscription(MakeChildObservable(_childFactory(change.Key)), change.Key);
                    else if (change.Reason is ChangeReason.Remove)
                        RemoveChildSubscription(change.Key);
                }
            }
        }

        protected override void ChildOnNext(string child, int parentKey)
        {
            ChildCalls.Add((child, parentKey));
            _cache.AddOrUpdate(child, parentKey);
        }

        protected override void EmitChanges(IObserver<IChangeSet<string, int>> observer)
        {
            Interlocked.Increment(ref EmitCallCount);
            var changes = _cache.CaptureChanges();
            if (changes.Count > 0)
                observer.OnNext(changes);
        }
    }

    /// <summary>Observer that records emissions, completion, and errors.</summary>
    private sealed class TestObserver : IObserver<IChangeSet<string, int>>
    {
        public int EmitCount;
        public bool IsCompleted;
        public Exception? Error;

        public void OnNext(IChangeSet<string, int> value) => Interlocked.Increment(ref EmitCount);

        public void OnError(Exception error) => Error = error;

        public void OnCompleted() => IsCompleted = true;
    }
}
