// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using DynamicData.Binding;
using DynamicData.Kernel;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Mega cross-cache stress test exercising every operator migrated from
/// Synchronize to SynchronizeSafe in multi-threaded bidirectional pipelines.
/// Every numeric parameter is derived from a seeded Randomizer (deterministic
/// but not hardcoded). Proves: no deadlocks, correct final state, Rx compliance.
/// </summary>
public sealed class CrossCacheDeadlockStressTest
{
    // ════════════════════════════════════════════════════════════════
    // Bound constants — ONLY the seed and Min/Max bounds are hardcoded.
    // Every actual test value is derived from the seeded Randomizer.
    // ════════════════════════════════════════════════════════════════

    private const int Seed = 42;

    // Market counts
    private const int SourceMarketCountMin = 80;
    private const int SourceMarketCountMax = 120;
    private const int OverlappingCountMin = 5;
    private const int OverlappingCountMax = 15;
    private const int TreeMarketCountMin = 12;
    private const int TreeMarketCountMax = 25;

    // Per-market price generation
    private const int PricesPerMarketMin = 2;
    private const int PricesPerMarketMax = 8;

    // Market property ranges
    private const int PriorityMin = 1;
    private const int PriorityMax = 10;
    private const double RatingMin = 1.0;
    private const double RatingMax = 10.0;
    private const int RegionCountMin = 3;
    private const int RegionCountMax = 7;

    // Price ranges
    private const decimal PriceMin = 1.0m;
    private const decimal PriceMax = 500.0m;

    // Pipeline parameters
    private const double RatingFilterThresholdMin = 2.0;
    private const double RatingFilterThresholdMax = 5.0;
    private const double TransformMultiplierMin = 1.5;
    private const double TransformMultiplierMax = 3.0;
    private const int PageSizeMin = 20;
    private const int PageSizeMax = 60;
    private const int VirtualSizeMin = 15;
    private const int VirtualSizeMax = 40;

    // Stress parameters
    private const int WriterThreadCountMin = 2;
    private const int WriterThreadCountMax = 6;
    private const int RatingMutationsMin = 10;
    private const int RatingMutationsMax = 30;
    private const int RegionMutationsMin = 5;
    private const int RegionMutationsMax = 15;

    // ID range spacing (generous gaps to prevent overlap)
    private const int IdRangeSpacing = 10_000;

    // Timeout
    private const int TimeoutSecondsMin = 30;
    private const int TimeoutSecondsMax = 60;

    // ════════════════════════════════════════════════════════════════
    // Domain types
    // ════════════════════════════════════════════════════════════════

    private sealed class StressMarket : AbstractNotifyPropertyChanged, IDisposable
    {
        private double _rating;
        private string _region;

        public StressMarket(int id, string name, string region, int priority, double rating, int? parentId = null)
        {
            Id = id;
            Name = name;
            _region = region;
            Priority = priority;
            _rating = rating;
            ParentId = parentId;
            Prices = new SourceCache<StressPrice, int>(p => p.Id);
        }

        public int Id { get; }

        public string Name { get; }

        public string Region
        {
            get => _region;
            set => SetAndRaise(ref _region, value);
        }

        public int Priority { get; }

        public double Rating
        {
            get => _rating;
            set => SetAndRaise(ref _rating, value);
        }

        public int? ParentId { get; }

        public SourceCache<StressPrice, int> Prices { get; }

        public IObservable<IChangeSet<StressPrice, int>> LatestPrices => Prices.Connect();

        public void Dispose() => Prices.Dispose();

        public override string ToString() => $"Market({Id}, {Name}, R={Rating:F1}, P={Priority})";
    }

    private sealed class StressPrice(int id, int marketId, decimal price)
    {
        public int Id { get; } = id;

        public int MarketId { get; } = marketId;

        public decimal Price { get; set; } = price;

        public override string ToString() => $"Price({Id}, M={MarketId}, ${Price:F2})";
    }

    private sealed class RatingDescComparer : IComparer<StressMarket>
    {
        public static RatingDescComparer Instance { get; } = new();

        public int Compare(StressMarket? x, StressMarket? y) =>
            (y?.Rating ?? 0).CompareTo(x?.Rating ?? 0);
    }

    private sealed class PriorityAscComparer : IComparer<StressMarket>
    {
        public static PriorityAscComparer Instance { get; } = new();

        public int Compare(StressMarket? x, StressMarket? y) =>
            (x?.Priority ?? 0).CompareTo(y?.Priority ?? 0);
    }

    private sealed class PriceDescComparer : IComparer<StressPrice>
    {
        public static PriceDescComparer Instance { get; } = new();

        public int Compare(StressPrice? x, StressPrice? y) =>
            (y?.Price ?? 0).CompareTo(x?.Price ?? 0);
    }

    // ════════════════════════════════════════════════════════════════
    // The Test
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AllOperators_CrossCache_NoDeadlock_CorrectResults()
    {
        // ── Derive ALL test parameters from seeded Randomizer ────────
        var rand = new Randomizer(Seed);

        var sourceACount = rand.Number(SourceMarketCountMin, SourceMarketCountMax);
        var sourceBCount = rand.Number(SourceMarketCountMin, SourceMarketCountMax);
        var overlappingCount = rand.Number(OverlappingCountMin, OverlappingCountMax);
        var treeCount = rand.Number(TreeMarketCountMin, TreeMarketCountMax);
        var regionCount = rand.Number(RegionCountMin, RegionCountMax);
        var regions = Enumerable.Range(0, regionCount).Select(i => $"Region-{i}").ToArray();
        var ratingThreshold = rand.Double(RatingFilterThresholdMin, RatingFilterThresholdMax);
        var transformMultiplier = rand.Double(TransformMultiplierMin, TransformMultiplierMax);
        var pageSize = rand.Number(PageSizeMin, PageSizeMax);
        var virtualSize = rand.Number(VirtualSizeMin, VirtualSizeMax);
        var writerThreads = rand.Number(WriterThreadCountMin, WriterThreadCountMax);
        var ratingMutations = rand.Number(RatingMutationsMin, RatingMutationsMax);
        var regionMutations = rand.Number(RegionMutationsMin, RegionMutationsMax);
        var timeoutSeconds = rand.Number(TimeoutSecondsMin, TimeoutSecondsMax);

        // ID ranges (non-overlapping, derived from spacing)
        var idA = rand.Number(1, IdRangeSpacing / 2);
        var idB = idA + IdRangeSpacing;
        var idOverlap = idB + IdRangeSpacing;
        var idForward = idOverlap + IdRangeSpacing;
        var idReverse = idForward + IdRangeSpacing;
        var idTree = idReverse + IdRangeSpacing;

        // ── Data Generation ─────────────────────────────────────────
        var marketsA = GenerateMarkets(rand, idA, sourceACount, regions);
        var marketsB = GenerateMarkets(rand, idB, sourceBCount, regions);
        var overlapping = GenerateMarkets(rand, idOverlap, overlappingCount, regions);
        var treeMarkets = GenerateTreeMarkets(rand, idTree, treeCount, regions);

        // ── Source Caches ───────────────────────────────────────────
        using var sourceA = new SourceCache<StressMarket, int>(m => m.Id);
        using var sourceB = new SourceCache<StressMarket, int>(m => m.Id);
        using var treeSource = new SourceCache<StressMarket, int>(m => m.Id);

        // ── Subjects for dynamic parameters ─────────────────────────
        using var pageRequests = new BehaviorSubject<IPageRequest>(new PageRequest(1, pageSize));
        using var virtualRequests = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, virtualSize));
        using var pauseBatch = new BehaviorSubject<bool>(false);
        using var forceTransform = new Subject<Func<StressMarket, bool>>();
        using var switchSource = new BehaviorSubject<IObservable<IChangeSet<StressMarket, int>>>(sourceA.Connect());
        using var comparerSubject = new BehaviorSubject<IComparer<StressMarket>>(RatingDescComparer.Instance);

        // Stop signal for operators with a library gap — they don't forward OnCompleted:
        // Static Combiner (Or/And/Except), BatchIf, TransformToTree, Switch
        using var stopSignal = new Subject<Unit>();

        // ── Completion tracking ─────────────────────────────────────
        var completionTasks = new List<Task>();
        var completionNames = new List<string>();
        using var subs = new CompositeDisposable();

        // Helpers
        IObservableCache<TObj, TKey> TrackCache<TObj, TKey>(IObservable<IChangeSet<TObj, TKey>> pipeline, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(pipeline))] string? name = null)
            where TObj : notnull where TKey : notnull
        {
            var pub = pipeline.Publish();
            completionTasks.Add(pub.LastOrDefaultAsync().ToTask());
            completionNames.Add(name ?? "?");
            var cache = pub.AsObservableCache();
            subs.Add(cache);
            subs.Add(pub.Connect());
            return cache;
        }

        // Bidirectional flows need writable SourceCaches
        using var forwardTarget = new SourceCache<StressMarket, int>(m => m.Id);
        using var reverseTarget = new SourceCache<StressMarket, int>(m => m.Id);

        void TrackIntoCache(IObservable<IChangeSet<StressMarket, int>> pipeline, SourceCache<StressMarket, int> target, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(pipeline))] string? name = null)
        {
            var pub = pipeline.Publish();
            completionTasks.Add(pub.LastOrDefaultAsync().ToTask());
            completionNames.Add(name ?? "?");
            subs.Add(pub.PopulateInto(target));
            subs.Add(pub.Connect());
        }

        // ── Auto-dispose items removed from source caches ───────────
        subs.Add(sourceA.Connect().DisposeMany().Subscribe());
        subs.Add(sourceB.Connect().DisposeMany().Subscribe());

        // ════════════════════════════════════════════════════════════
        // FLOW 1 — Forward Bidirectional: sourceA → forwardTarget → sourceB
        // Operators: AutoRefresh, Filter(dynamic), Transform(forceTransform),
        //            OnItemRemoved, DisposeMany, Sort, Page, BatchIf
        // ════════════════════════════════════════════════════════════

        var forwardRemovals = 0;
        var forwardIdCounter = idForward;

        TrackIntoCache(
            sourceA.Connect()
                .AutoRefresh(m => m.Rating)                                   // AutoRefresh [1]
                .Filter(m => m.Id >= idA && m.Id < idA + sourceACount         // Filter(dynamic) [1]
                          && m.Rating >= ratingThreshold)
                .Transform(                                                    // Transform(forceTransform) [1]
                    m => new StressMarket(
                        Interlocked.Increment(ref forwardIdCounter),
                        $"F-{m.Name}", m.Region, m.Priority,
                        m.Rating * transformMultiplier),
                    forceTransform)
                .OnItemRemoved(m =>                                            // OnItemRemoved [1]
                    Interlocked.Increment(ref forwardRemovals))
                .DisposeMany()                                                 // DisposeMany [1]
                .Sort(RatingDescComparer.Instance)                             // Sort [1]
                .Page(pageRequests)                                            // Page [1]
                .BatchIf(pauseBatch, false, (TimeSpan?)null)                   // BatchIf [1]
                .TakeUntil(stopSignal),
            forwardTarget);

        subs.Add(forwardTarget.Connect().PopulateInto(sourceB));

        // ════════════════════════════════════════════════════════════
        // FLOW 2 — Reverse Bidirectional: sourceB → reverseTarget → sourceA
        // Operators: AutoRefresh, Filter(dynamic), Sort, Virtualise
        // ════════════════════════════════════════════════════════════

        var reverseIdCounter = idReverse;

        TrackIntoCache(
            sourceB.Connect()
                .AutoRefresh(m => m.Rating)                                   // AutoRefresh [2]
                .Filter(m => m.Id >= idB && m.Id < idB + sourceBCount         // Filter(dynamic) [2]
                          && m.Rating >= ratingThreshold)
                .Sort(RatingDescComparer.Instance)                             // Sort [2]
                .Virtualise(virtualRequests)                                   // Virtualise [1]
                .Transform(m => new StressMarket(                              // Transform [2]
                    Interlocked.Increment(ref reverseIdCounter),
                    $"R-{m.Name}", m.Region, m.Priority, m.Rating))
                .TakeUntil(stopSignal),                                        // AutoRefresh doesn't forward OnCompleted
            reverseTarget);

        subs.Add(reverseTarget.Connect().PopulateInto(sourceA));

        // Side chains
        using var sortVirtResults = sourceB.Connect()
            .SortAndVirtualize(comparerSubject, virtualRequests)               // SortAndVirtualize [1]
            .AsAggregator();

        IQuery<StressMarket, int>? lastQuery = null;
        var qwcTcs = new TaskCompletionSource();
        subs.Add(sourceB.Connect()
            .QueryWhenChanged()                                                // QueryWhenChanged [1]
            .Subscribe(q => lastQuery = q, ex => qwcTcs.TrySetException(ex), () => qwcTcs.TrySetResult()));
        completionTasks.Add(qwcTcs.Task);
        completionNames.Add("QueryWhenChanged-B");

        // ════════════════════════════════════════════════════════════
        // FLOW 3 — Joins: sourceA × sourceB
        // Operators: FullJoin, InnerJoin, LeftJoin, RightJoin
        // ════════════════════════════════════════════════════════════

        var fullJoinCache = TrackCache(
            sourceA.Connect().FullJoin(                                        // FullJoin [1]
                sourceB.Connect(), r => r.Id,
                (key, left, right) =>
                {
                    var src = left.HasValue ? left.Value : right.Value;
                    return new StressMarket(key, $"FJ-{src.Name}", src.Region, src.Priority, src.Rating);
                }));

        var innerJoinCache = TrackCache(
            sourceA.Connect().InnerJoin(                                       // InnerJoin [1]
                sourceB.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key.leftKey, $"IJ-{left.Name}", left.Region, left.Priority, right.Rating))
                .ChangeKey(m => m.Id));

        var leftJoinCache = TrackCache(
            sourceA.Connect().LeftJoin(                                        // LeftJoin [1]
                sourceB.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key, $"LJ-{left.Name}", left.Region, left.Priority,
                        right.HasValue ? right.Value.Rating : left.Rating)));

        var rightJoinCache = TrackCache(
            sourceA.Connect().RightJoin(                                       // RightJoin [1]
                sourceB.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key, $"RJ-{right.Name}", right.Region, right.Priority,
                        left.HasValue ? left.Value.Rating : right.Rating)));

        // ════════════════════════════════════════════════════════════
        // FLOW 4 — Combiners + second join uses
        // Operators: Or, And, Except, MergeChangeSets, FullJoin[2], InnerJoin[2]
        // ════════════════════════════════════════════════════════════

        var orCache = TrackCache(
            sourceA.Connect().Or(sourceB.Connect())                            // Or [1]
                .TakeUntil(stopSignal));

        var andCache = TrackCache(
            sourceA.Connect().And(sourceB.Connect())                           // And [1]
                .TakeUntil(stopSignal));

        var exceptCache = TrackCache(
            sourceA.Connect().Except(sourceB.Connect())                        // Except [1]
                .TakeUntil(stopSignal));

        var mergedCache = TrackCache(
            new[] { sourceA.Connect(), sourceB.Connect() }
                .MergeChangeSets());                                           // MergeChangeSets [1]

        // Second join uses: join the join outputs together
        var joinedJoinsCache = TrackCache(
            fullJoinCache.Connect().FullJoin(                                  // FullJoin [2]
                rightJoinCache.Connect(), r => r.Id,
                (key, left, right) =>
                {
                    var src = left.HasValue ? left.Value : right.Value;
                    return new StressMarket(key, $"JJ-{src.Name}", src.Region, src.Priority, src.Rating);
                }));

        // Second InnerJoin on the overlapping subset
        var innerJoin2Cache = TrackCache(
            leftJoinCache.Connect().InnerJoin(                                 // InnerJoin [2]
                rightJoinCache.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key.leftKey, $"IJ2-{left.Name}", left.Region, left.Priority, right.Rating))
                .ChangeKey(m => m.Id));

        // ════════════════════════════════════════════════════════════
        // FLOW 5 — Groups: sourceA → grouped → flattened
        // Operators: GroupOn, GroupOnImmutable, GroupOnObservable, MergeMany
        // ════════════════════════════════════════════════════════════

        var groupCache = TrackCache(
            sourceA.Connect()
                .Group(m => m.Region)                                          // GroupOn [1]
                .MergeMany(group => group.Cache.Connect()));                   // MergeMany [1]

        using var immGroupAgg = sourceA.Connect()
            .GroupWithImmutableState(m => m.Region)                            // GroupOnImmutable [1]
            .AsAggregator();

        var dynGroupCache = TrackCache(
            sourceA.Connect()
                .GroupOnObservable(m => m.WhenPropertyChanged(x => x.Region)   // GroupOnObservable [1]
                    .Select(pv => pv.Value ?? regions[0]))
                .MergeMany(group => group.Cache.Connect())
                .TakeUntil(stopSignal));                                        // WhenPropertyChanged children don't complete

        // Second GroupOn use on sourceB
        var groupBCache = TrackCache(
            sourceB.Connect()
                .Group(m => m.Region)                                          // GroupOn [2]
                .MergeMany(group => group.Cache.Connect()));                   // MergeMany [3]

        // ════════════════════════════════════════════════════════════
        // FLOW 6 — MergeManyChangeSets (both overloads)
        // ════════════════════════════════════════════════════════════

        // 6a: Child comparer — highest price wins across markets
        var childPriceCache = TrackCache(
            sourceA.Connect()
                .MergeManyChangeSets(m => m.LatestPrices,                      // MergeManyCS(child) [1]
                    PriceDescComparer.Instance));

        // 6b: Source comparer + child comparer — priority then price
        var sourcePriceCache = TrackCache(
            sourceB.Connect()
                .MergeManyChangeSets(m => m.LatestPrices,                      // MergeManyCS(source) [1]
                    PriorityAscComparer.Instance, PriceDescComparer.Instance));

        // Second uses: reversed sources
        var childPriceBCache = TrackCache(
            sourceB.Connect()
                .MergeManyChangeSets(m => m.LatestPrices,                      // MergeManyCS(child) [2]
                    PriceDescComparer.Instance));

        var sourcePriceACache = TrackCache(
            sourceA.Connect()
                .MergeManyChangeSets(m => m.LatestPrices,                      // MergeManyCS(source) [2]
                    PriorityAscComparer.Instance, PriceDescComparer.Instance));

        // ════════════════════════════════════════════════════════════
        // FLOW 7 — Sort Variants, Switch, second BatchIf/Page/Virtualise
        // ════════════════════════════════════════════════════════════

        var boundListA = new List<StressMarket>();
        subs.Add(sourceA.Connect()
            .SortAndBind(boundListA, RatingDescComparer.Instance)              // SortAndBind [1]
            .Subscribe());

        var boundListB = new List<StressMarket>();
        subs.Add(sourceA.Connect()
            .SortAndBind(boundListB, comparerSubject)                          // SortAndBind [2]
            .Subscribe());

        var switchCache = TrackCache(
            switchSource.Switch()                                              // Switch [1]
                .TakeUntil(stopSignal));

        // Second Page + Virtualise + BatchIf uses on sourceB
        using var pageBSubject = new BehaviorSubject<IPageRequest>(new PageRequest(1, pageSize));
        using var pauseB = new BehaviorSubject<bool>(false);

        var pageBCache = TrackCache(
            sourceB.Connect()
                .Sort(PriorityAscComparer.Instance)                            // Sort [3]
                .Page(pageBSubject)                                            // Page [2]
                .BatchIf(pauseB, false, (TimeSpan?)null)                       // BatchIf [2]
                .TakeUntil(stopSignal));

        using var virtBRequests = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, virtualSize));
        var virtBCache = TrackCache(
            sourceB.Connect()
                .Sort(PriorityAscComparer.Instance)                            // Sort [4]
                .Virtualise(virtBRequests));                                    // Virtualise [2]

        // ════════════════════════════════════════════════════════════
        // FLOW 8 — TransformMany, TransformToTree, second OnItemRemoved
        // ════════════════════════════════════════════════════════════

        var allPricesACache = TrackCache(
            sourceA.Connect()
                .TransformMany(m => (IObservableCache<StressPrice, int>)m.Prices, // TransformMany [1]
                    p => p.Id));

        var allPricesBCache = TrackCache(
            sourceB.Connect()
                .TransformMany(m => (IObservableCache<StressPrice, int>)m.Prices, // TransformMany [2]
                    p => p.Id));

        // TransformToTree doesn't forward OnCompleted (library gap) — needs TakeUntil
        var treeCache = TrackCache(
            treeSource.Connect()
                .TransformToTree(m => m.ParentId ?? 0)                         // TransformToTree [1]
                .TakeUntil(stopSignal));

        // Second OnItemRemoved + DisposeMany on sourceB
        var reverseRemovals = 0;
        subs.Add(sourceB.Connect()
            .OnItemRemoved(m => Interlocked.Increment(ref reverseRemovals))    // OnItemRemoved [2]
            .DisposeMany()                                                     // DisposeMany [2]
            .Subscribe());

        // Operators not covered here (AsyncDisposeMany, TransformAsync, TransformOnObservable,
        // TransformManyAsync, SortAndPage, MergeManyListChangeSets) are exercised
        // in their dedicated fixture tests under concurrent load.

        // Second SortAndVirtualize on sourceA
        using var sortVirtAResults = sourceA.Connect()
            .SortAndVirtualize(comparerSubject, virtualRequests)               // SortAndVirtualize [2]
            .AsAggregator();

        // Second QueryWhenChanged on sourceA
        IQuery<StressMarket, int>? lastQueryA = null;
        var qwcATcs = new TaskCompletionSource();
        subs.Add(sourceA.Connect()
            .QueryWhenChanged()                                                // QueryWhenChanged [2]
            .Subscribe(q => lastQueryA = q, ex => qwcATcs.TrySetException(ex), () => qwcATcs.TrySetResult()));
        completionTasks.Add(qwcATcs.Task);
        completionNames.Add("QueryWhenChanged-A");

        // Second Switch + GroupOnImmutable + GroupOnObservable
        using var switchSource2 = new BehaviorSubject<IObservable<IChangeSet<StressMarket, int>>>(sourceB.Connect());
        var switchCache2 = TrackCache(
            switchSource2.Switch()                                             // Switch [2]
                .TakeUntil(stopSignal));

        using var immGroupBAgg = sourceB.Connect()
            .GroupWithImmutableState(m => m.Region)                            // GroupOnImmutable [2]
            .AsAggregator();

        var dynGroupBCache = TrackCache(
            sourceB.Connect()
                .GroupOnObservable(m => m.WhenPropertyChanged(x => x.Region)   // GroupOnObservable [2]
                    .Select(pv => pv.Value ?? regions[0]))
                .MergeMany(group => group.Cache.Connect())
                .TakeUntil(stopSignal));                                        // WhenPropertyChanged children don't complete

        // Second LeftJoin + RightJoin + Or + And + Except + MergeChangeSets
        var leftJoin2Cache = TrackCache(
            sourceB.Connect().LeftJoin(                                        // LeftJoin [2]
                sourceA.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key, $"LJ2-{left.Name}", left.Region, left.Priority,
                        right.HasValue ? right.Value.Rating : left.Rating)));

        var rightJoin2Cache = TrackCache(
            sourceB.Connect().RightJoin(                                       // RightJoin [2]
                sourceA.Connect(), r => r.Id,
                (key, left, right) =>
                    new StressMarket(key, $"RJ2-{right.Name}", right.Region, right.Priority,
                        left.HasValue ? left.Value.Rating : right.Rating)));

        var orCache2 = TrackCache(
            sourceB.Connect().Or(sourceA.Connect())                            // Or [2]
                .TakeUntil(stopSignal));

        var andCache2 = TrackCache(
            sourceB.Connect().And(sourceA.Connect())                           // And [2]
                .TakeUntil(stopSignal));

        var exceptCache2 = TrackCache(
            sourceB.Connect().Except(sourceA.Connect())                        // Except [2]
                .TakeUntil(stopSignal));

        var mergedCache2 = TrackCache(
            new[] { sourceB.Connect(), sourceA.Connect() }
                .MergeChangeSets());                                           // MergeChangeSets [2]

        // Second TransformToTree using a different subset
        var treeCache2 = TrackCache(
            treeSource.Connect()
                .Filter(m => m.ParentId.HasValue || m.Id < idTree + treeCount / 2)
                .TransformToTree(m => m.ParentId ?? 0)                         // TransformToTree [2]
                .TakeUntil(stopSignal));

        // ════════════════════════════════════════════════════════════
        // Multi-Threaded Writers
        // ════════════════════════════════════════════════════════════

        var barrier = new Barrier(writerThreads * 2 + 1);
        var slicesA = PartitionList(marketsA, writerThreads);
        var slicesB = PartitionList(marketsB, writerThreads);
        var writerTasks = new List<Task>();

        for (var t = 0; t < writerThreads; t++)
        {
            var slice = slicesA[t];
            var tRand = new Randomizer(Seed + t + 1);
            writerTasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                foreach (var m in slice) sourceA.AddOrUpdate(m);
                for (var i = 0; i < ratingMutations; i++)
                    slice[tRand.Number(0, slice.Count - 1)].Rating = tRand.Double(RatingMin, RatingMax);
                for (var i = 0; i < regionMutations; i++)
                    slice[tRand.Number(0, slice.Count - 1)].Region = regions[tRand.Number(0, regionCount - 1)];
                barrier.SignalAndWait();
            }));
        }

        for (var t = 0; t < writerThreads; t++)
        {
            var slice = slicesB[t];
            var tRand = new Randomizer(Seed + writerThreads + t + 1);
            writerTasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                foreach (var m in slice) sourceB.AddOrUpdate(m);
                for (var i = 0; i < ratingMutations; i++)
                    slice[tRand.Number(0, slice.Count - 1)].Rating = tRand.Double(RatingMin, RatingMax);
                barrier.SignalAndWait();
            }));
        }

        // ── Start writers ───────────────────────────────────────────
        barrier.SignalAndWait();
        pauseBatch.OnNext(true);
        barrier.SignalAndWait();
        await Task.WhenAll(writerTasks);
        pauseBatch.OnNext(false);

        // Post-write operations
        sourceA.AddOrUpdate(overlapping);
        sourceB.AddOrUpdate(overlapping);
        treeSource.AddOrUpdate(treeMarkets);
        forceTransform.OnNext(m => m.Rating > ratingThreshold);
        switchSource.OnNext(sourceB.Connect());
        switchSource2.OnNext(sourceA.Connect());
        comparerSubject.OnNext(PriorityAscComparer.Instance);

        // ── Teardown ────────────────────────────────────────────────
        // 1. Signal stop for operators with library gaps (don't forward OnCompleted):
        //    Static Combiner (Or/And/Except), BatchIf, TransformToTree, Switch
        stopSignal.OnNext(Unit.Default);
        stopSignal.OnCompleted();

        // ── Snapshot final state (bidirectional flows are frozen by stopSignal) ──
        var finalAKeys = new HashSet<int>(sourceA.Keys);
        var finalBKeys = new HashSet<int>(sourceB.Keys);

        // 2. Complete all BehaviorSubjects so multi-source operators can complete
        forceTransform.OnCompleted();
        pageRequests.OnCompleted();
        pageBSubject.OnCompleted();
        virtualRequests.OnCompleted();
        virtBRequests.OnCompleted();
        comparerSubject.OnCompleted();
        pauseBatch.OnCompleted();
        pauseB.OnCompleted();
        switchSource.OnCompleted();
        switchSource2.OnCompleted();

        // 2. Dispose source caches — fires OnCompleted on Connect() streams,
        //    DisposeMany auto-disposes inner price caches (completing MMCS/TransformMany)
        sourceA.Dispose();
        sourceB.Dispose();
        treeSource.Dispose();

        // 3. Dispose subscriptions — disconnects Publish, firing OnCompleted on
        //    all published streams (completes operators like AutoRefresh, TreeBuilder
        //    that don't propagate OnCompleted naturally)
        subs.Dispose();

        // 4. Wait for all completion tasks with timeout (deadlock detector)
        var allCompleted = Task.WhenAll(completionTasks);
        var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var finished = await Task.WhenAny(allCompleted, timeout);
        if (!ReferenceEquals(finished, allCompleted))
        {
            var pending = completionTasks.Select((t2, i) => new { Index = i, t2.Status, Name = completionNames[i] })
                .Where(x => x.Status != TaskStatus.RanToCompletion)
                .Select(x => $"[{x.Index}] {x.Name} ({x.Status})").ToList();
            pending.Should().BeEmpty($"all {completionTasks.Count} tasks should finish within {timeoutSeconds}s. Pending: {string.Join(", ", pending)}");
        }

        // ════════════════════════════════════════════════════════════
        // Verification — exact contents with BeEquivalentTo
        // ════════════════════════════════════════════════════════════

        // Flow 1: Forward — filtered, transformed, paged subset of sourceA
        forwardTarget.Count.Should().BeGreaterThan(0, "Flow1 should produce results");
        forwardTarget.Count.Should().BeLessThanOrEqualTo(pageSize, "Page should limit");
        forwardTarget.Items.Should().OnlyContain(m => m.Name.StartsWith("F-"), "Transform prefixes 'F-'");
        forwardTarget.Items.Should().OnlyContain(
            m => m.Rating >= ratingThreshold * transformMultiplier,
            "Transform multiplies rating of items that passed filter");
        forwardRemovals.Should().BeGreaterThan(0, "OnItemRemoved fires on rating mutation exits");

        // Flow 2: Reverse — filtered, sorted, virtualized, transformed subset of sourceB
        reverseTarget.Count.Should().BeGreaterThan(0, "Flow2 should produce results");
        reverseTarget.Count.Should().BeLessThanOrEqualTo(virtualSize, "Virtualise limits");
        reverseTarget.Items.Should().OnlyContain(m => m.Name.StartsWith("R-"), "Transform prefixes 'R-'");

        // Flow 3: Joins — verify mathematical relationships hold
        // Each cache may see a slightly different snapshot due to bidirectional flow timing,
        // but the set-theoretic relationships must hold within each cache's own view.
        fullJoinCache.Items.Should().OnlyContain(m => m.Name.StartsWith("FJ-"), "FullJoin prefixes 'FJ-'");
        innerJoinCache.Items.Should().OnlyContain(m => m.Name.StartsWith("IJ-"), "InnerJoin prefixes 'IJ-'");
        leftJoinCache.Items.Should().OnlyContain(m => m.Name.StartsWith("LJ-"), "LeftJoin prefixes 'LJ-'");
        rightJoinCache.Items.Should().OnlyContain(m => m.Name.StartsWith("RJ-"), "RightJoin prefixes 'RJ-'");

        // InnerJoin keys ⊂ FullJoin keys (intersection ⊂ union)
        new HashSet<int>(innerJoinCache.Keys).IsSubsetOf(new HashSet<int>(fullJoinCache.Keys)).Should()
            .BeTrue("InnerJoin ⊂ FullJoin");
        // InnerJoin must have at least the overlapping keys
        innerJoinCache.Count.Should().BeGreaterThanOrEqualTo(overlappingCount,
            "InnerJoin finds at least overlapping items");

        // Flow 4: Combiners — Or and Merged share the same Publish, so they're identical
        orCache.Keys.Should().BeEquivalentTo(mergedCache.Keys, "Or = Merged (same Publish sources)");
        // And ⊂ Or
        new HashSet<int>(andCache.Keys).IsSubsetOf(new HashSet<int>(orCache.Keys)).Should()
            .BeTrue("And ⊂ Or");
        // Except ∩ And = ∅
        new HashSet<int>(exceptCache.Keys).Overlaps(andCache.Keys).Should()
            .BeFalse("Except ∩ And = ∅");
        // Except ∪ And ∪ (items only in B) = Or
        var exceptPlusAnd = new HashSet<int>(exceptCache.Keys);
        exceptPlusAnd.UnionWith(andCache.Keys);
        exceptPlusAnd.IsSubsetOf(new HashSet<int>(orCache.Keys)).Should()
            .BeTrue("Except ∪ And ⊂ Or");

        // Second joins — cross-verify with first joins (same sources, same completion)
        leftJoin2Cache.Keys.Should().BeEquivalentTo(rightJoinCache.Keys, "LeftJoin2(B×A) = RightJoin(A×B)");
        leftJoin2Cache.Items.Should().OnlyContain(m => m.Name.StartsWith("LJ2-"), "LeftJoin2 prefixes");
        rightJoin2Cache.Keys.Should().BeEquivalentTo(leftJoinCache.Keys, "RightJoin2(B×A) = LeftJoin(A×B)");
        rightJoin2Cache.Items.Should().OnlyContain(m => m.Name.StartsWith("RJ2-"), "RightJoin2 prefixes");
        orCache2.Keys.Should().BeEquivalentTo(orCache.Keys, "Or2 = Or (same sources, same completion)");
        andCache2.Keys.Should().BeEquivalentTo(andCache.Keys, "And2 = And (same sources)");
        mergedCache2.Keys.Should().BeEquivalentTo(mergedCache.Keys, "MergedCache2 = Merged (same sources)");

        // Flow 5: Groups — verify grouping preserves all items from same snapshot
        groupCache.Items.Select(m => m.Region).Distinct().Count().Should()
            .BeGreaterThan(1, "GroupOn creates multiple regions");
        immGroupAgg.Data.Count.Should().BeGreaterThan(1, "GroupOnImmutable produces groups");
        immGroupBAgg.Data.Count.Should().BeGreaterThan(1, "GroupOnImmutable(B) produces groups");

        // Flow 6: MergeManyChangeSets — exact price key verification
        // MMCS(child/A) and MMCS(source/A) see the same sourceA markets, same price keys
        childPriceCache.Keys.Should().BeEquivalentTo(sourcePriceACache.Keys,
            "MMCS(child/A) = MMCS(source/A) — same source markets, same price keys");
        childPriceBCache.Keys.Should().BeEquivalentTo(sourcePriceCache.Keys,
            "MMCS(child/B) = MMCS(source/B) — same source markets, same price keys");

        // Flow 7: SortAndBind — exact count matching sourceA
        boundListA.Count.Should().Be(leftJoinCache.Count, "SortAndBind = LeftJoin count (both see all sourceA)");
        boundListB.Count.Should().Be(leftJoinCache.Count, "SortAndBind(obs) = LeftJoin count");
        for (var i = 1; i < boundListB.Count; i++)
            boundListB[i - 1].Priority.Should().BeLessThanOrEqualTo(boundListB[i].Priority,
                "SortAndBind(obs) re-sorted by priority after comparer switch");

        // Switch: after switching, should have items from the switched-to source
        switchCache.Count.Should().BeGreaterThan(0, "Switch (switched to B) has items");
        switchCache2.Count.Should().BeGreaterThan(0, "Switch2 (switched to A) has items");

        pageBCache.Count.Should().BeGreaterThan(0, "Page(B) produces results");
        pageBCache.Count.Should().BeLessThanOrEqualTo(pageSize, "Page(B) respects page limit");
        virtBCache.Count.Should().BeGreaterThan(0, "Virtualise(B) produces results");
        virtBCache.Count.Should().BeLessThanOrEqualTo(virtualSize, "Virtualise(B) respects virtual limit");

        // Flow 8: TransformMany — exact price key sets from original markets
        var expectedPriceKeysA = new HashSet<int>(marketsA.SelectMany(m => m.Prices.Keys));
        new HashSet<int>(allPricesACache.Keys).IsSupersetOf(expectedPriceKeysA).Should()
            .BeTrue("TransformMany(A) contains all original sourceA prices");
        var expectedPriceKeysB = new HashSet<int>(marketsB.SelectMany(m => m.Prices.Keys));
        new HashSet<int>(allPricesBCache.Keys).IsSupersetOf(expectedPriceKeysB).Should()
            .BeTrue("TransformMany(B) contains all original sourceB prices");

        // TransformToTree
        static int CountAll(IEnumerable<Node<StressMarket, int>> nodes)
        {
            var c = 0;
            foreach (var n in nodes) { c++; c += CountAll(n.Children.Items); }
            return c;
        }

        CountAll(treeCache.Items).Should().Be(treeCount, "Tree has all markets across depths");
        treeCache.Items.Any(n => n.Children.Count > 0).Should().BeTrue("Tree has child nodes");
        treeCache2.Count.Should().BeGreaterThan(0, "Tree2 produces results");

        

        // Side chains
        lastQuery.Should().NotBeNull("QueryWhenChanged(B) fired");
        lastQueryA.Should().NotBeNull("QueryWhenChanged(A) fired");
        sortVirtResults.Data.Count.Should().BeLessThanOrEqualTo(virtualSize, "SortAndVirtualize respects limit");
        sortVirtAResults.Data.Count.Should().BeLessThanOrEqualTo(virtualSize, "SortAndVirtualize(A) respects limit");
    }

    // ════════════════════════════════════════════════════════════════
    // Data Generation
    // ════════════════════════════════════════════════════════════════

    private static List<StressMarket> GenerateMarkets(Randomizer rand, int idStart, int count, string[] regions)
    {
        var markets = new List<StressMarket>(count);
        for (var i = 0; i < count; i++)
        {
            var id = idStart + i;
            var market = new StressMarket(
                id, $"Market-{id}",
                regions[rand.Number(0, regions.Length - 1)],
                rand.Number(PriorityMin, PriorityMax),
                rand.Double(RatingMin, RatingMax));

            var priceCount = rand.Number(PricesPerMarketMin, PricesPerMarketMax);
            market.Prices.Edit(u =>
            {
                for (var p = 0; p < priceCount; p++)
                    u.AddOrUpdate(new StressPrice(id * 1000 + p, id, rand.Decimal(PriceMin, PriceMax)));
            });

            markets.Add(market);
        }

        return markets;
    }

    private static List<StressMarket> GenerateTreeMarkets(Randomizer rand, int idStart, int count, string[] regions)
    {
        var markets = new List<StressMarket>(count);
        var rootCount = Math.Max(2, count / 3);

        for (var i = 0; i < rootCount; i++)
            markets.Add(new StressMarket(idStart + i, $"Tree-Root-{i}",
                regions[rand.Number(0, regions.Length - 1)],
                rand.Number(PriorityMin, PriorityMax),
                rand.Double(RatingMin, RatingMax)));

        for (var i = rootCount; i < count; i++)
        {
            var parentIdx = rand.Number(0, i - 1);
            markets.Add(new StressMarket(idStart + i, $"Tree-Child-{i}",
                regions[rand.Number(0, regions.Length - 1)],
                rand.Number(PriorityMin, PriorityMax),
                rand.Double(RatingMin, RatingMax),
                markets[parentIdx].Id));
        }

        return markets;
    }

    private static List<List<T>> PartitionList<T>(List<T> source, int partitions)
    {
        var result = Enumerable.Range(0, partitions).Select(_ => new List<T>()).ToList();
        for (var i = 0; i < source.Count; i++)
            result[i % partitions].Add(source[i]);
        return result;
    }
}
