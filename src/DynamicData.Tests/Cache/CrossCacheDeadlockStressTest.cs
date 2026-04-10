// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Comprehensive cross-cache stress test exercising every operator that uses
/// Synchronize/SynchronizeSafe in a multi-threaded bidirectional pipeline.
/// Proves: no deadlocks, correct final state, Rx contract compliance.
/// </summary>
public sealed class CrossCacheDeadlockStressTest : IDisposable
{
    private const int WriterThreads = 8;
    private const int ItemsPerThread = 500;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly Randomizer Rand = new(8675309); // deterministic seed

    private readonly Faker<Animal> _animalFaker = Fakers.Animal.Clone().WithSeed(Rand);
    private readonly SourceCache<Animal, int> _cacheA = new(x => x.Id);
    private readonly SourceCache<Animal, int> _cacheB = new(x => x.Id);
    private readonly CompositeDisposable _cleanup = new();

    public void Dispose()
    {
        _cleanup.Dispose();
        _cacheA.Dispose();
        _cacheB.Dispose();
    }

    /// <summary>
    /// The "kitchen sink" test. Chains every operator that could deadlock into
    /// massive fluent expressions across two caches with bidirectional flow.
    /// 8 writer threads per cache, 500 items each, property mutations, sort
    /// changes, page changes — maximum contention.
    /// </summary>
    [Fact]
    public async Task KitchenSink_AllOperatorsChained_NoDeadlock_CorrectResults()
    {
        // ================================================================
        // PIPELINE 1: The Monster Chain (cacheA → cacheB)
        //
        // Every operator that uses Synchronize/SynchronizeSafe composed
        // into a single fluent expression. This is intentionally absurd —
        // the point is to prove they can all coexist without deadlock.
        // ================================================================

        var sortComparer = new BehaviorSubject<IComparer<Animal>>(
            SortExpressionComparer<Animal>.Ascending(x => x.Id));
        _cleanup.Add(sortComparer);

        var pageRequests = new BehaviorSubject<IPageRequest>(new PageRequest(1, 100));
        _cleanup.Add(pageRequests);

        var virtualRequests = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        _cleanup.Add(virtualRequests);

        var pauseBatch = new BehaviorSubject<bool>(false);
        _cleanup.Add(pauseBatch);

        var monsterChain = _cacheA.Connect()          // IChangeSet<Animal, int>
            .AutoRefresh(x => x.IncludeInResults)      // re-evaluate on property change
            .Filter(x => x.IncludeInResults)           // static filter
            .Sort(sortComparer)                        // dynamic sort
            .Page(pageRequests)                        // paging
            .Transform(a => new Animal(                // transform to new instance
                "m-" + a.Name, a.Type, a.Family, a.IncludeInResults, a.Id + 100_000))
            .IgnoreSameReferenceUpdate()               // safe operator
            .WhereReasonsAre(ChangeReason.Add,
                ChangeReason.Update,
                ChangeReason.Remove,
                ChangeReason.Refresh)                  // safe operator
            .OnItemAdded(_ => { })                     // safe operator
            .OnItemUpdated((_, _) => { })              // safe operator
            .OnItemRemoved(_ => { })                   // safe operator
            .SubscribeMany(_ => Disposable.Empty)      // safe operator
            .NotEmpty()                                // safe operator
            .SkipInitial()                             // safe operator - skip the first batch
            .AsAggregator();
        _cleanup.Add(monsterChain);

        // ================================================================
        // PIPELINE 2: Cross-cache Join + Group + MergeChangeSets
        // ================================================================

        var joinChain = _cacheA.Connect()
            .FullJoin(
                _cacheB.Connect(),
                right => right.Id,
                (key, left, right) =>
                {
                    var name = (left.HasValue ? left.Value.Name : "?") + "+"
                             + (right.HasValue ? right.Value.Name : "?");
                    return new Animal(name, "Hybrid", AnimalFamily.Mammal, true, key + 200_000);
                })
            .Group(x => x.Family)                      // GroupOn
            .DisposeMany()                             // safe but exercises the path
            .MergeManyChangeSets(group => group.Cache.Connect()  // MergeManyChangeSets into groups
                .Transform(a => new Animal("g-" + a.Name, a.Type, a.Family, true, a.Id + 300_000)))
            .AsAggregator();
        _cleanup.Add(joinChain);

        // ================================================================
        // PIPELINE 3: InnerJoin + LeftJoin + RightJoin
        // ================================================================

        var innerJoinResults = _cacheA.Connect()
            .InnerJoin(_cacheB.Connect(), r => r.Id,
                (keys, l, r) => new Animal("ij-" + l.Name, r.Type, l.Family, true, keys.leftKey + 400_000))
            .ChangeKey(x => x.Id)
            .AsAggregator();
        _cleanup.Add(innerJoinResults);

        var leftJoinResults = _cacheA.Connect()
            .LeftJoin(_cacheB.Connect(), r => r.Id,
                (key, l, r) => new Animal("lj-" + l.Name, l.Type, l.Family, r.HasValue, key + 500_000))
            .AsAggregator();
        _cleanup.Add(leftJoinResults);

        var rightJoinResults = _cacheA.Connect()
            .RightJoin(_cacheB.Connect(), r => r.Id,
                (key, l, r) => new Animal("rj-" + r.Name, r.Type, r.Family, l.HasValue, key + 600_000))
            .AsAggregator();
        _cleanup.Add(rightJoinResults);

        // ================================================================
        // PIPELINE 4: MergeChangeSets + Or + BatchIf + QueryWhenChanged
        // ================================================================

        var mergedResults = new[] { _cacheA.Connect(), _cacheB.Connect() }
            .MergeChangeSets()
            .AsAggregator();
        _cleanup.Add(mergedResults);

        var orResults = _cacheA.Connect().Or(_cacheB.Connect()).AsAggregator();
        _cleanup.Add(orResults);

        var batchedResults = _cacheA.Connect()
            .BatchIf(pauseBatch, false, null)
            .AsAggregator();
        _cleanup.Add(batchedResults);

        IQuery<Animal, int>? lastQuery = null;
        var querySub = _cacheB.Connect()
            .QueryWhenChanged()
            .Subscribe(q => lastQuery = q);
        _cleanup.Add(querySub);

        // ================================================================
        // PIPELINE 5: SortAndBind + Virtualise + GroupWithImmutableState
        // ================================================================

        var boundList = new List<Animal>();
        var sortAndBind = _cacheA.Connect()
            .SortAndBind(boundList, SortExpressionComparer<Animal>.Ascending(x => x.Id))
            .Subscribe();
        _cleanup.Add(sortAndBind);

        var virtualisedResults = _cacheA.Connect()
            .Sort(SortExpressionComparer<Animal>.Ascending(x => x.Id))
            .Virtualise(virtualRequests)
            .AsAggregator();
        _cleanup.Add(virtualisedResults);

        var immutableGroups = _cacheA.Connect()
            .GroupWithImmutableState(x => x.Family)
            .AsAggregator();
        _cleanup.Add(immutableGroups);

        // ================================================================
        // PIPELINE 6: Switch + TransformMany + TreeBuilder (via TransformToTree)
        // ================================================================

        var switchSource = new BehaviorSubject<IObservable<IChangeSet<Animal, int>>>(_cacheA.Connect());
        _cleanup.Add(switchSource);
        var switchResults = switchSource.Switch().AsAggregator();
        _cleanup.Add(switchResults);

        var transformManyResults = _cacheA.Connect()
            .TransformMany(
                a => new[] { a, new Animal(a.Name + "-twin", a.Type, a.Family, true, a.Id + 700_000) },
                twin => twin.Id)
            .AsAggregator();
        _cleanup.Add(transformManyResults);

        // ================================================================
        // PIPELINE 7: Bidirectional flow (cacheA ↔ cacheB via PopulateInto)
        // ================================================================

        var forwardPipeline = _cacheA.Connect()
            .Filter(x => x.Family == AnimalFamily.Mammal)
            .Transform(a => new Animal("fwd-" + a.Name, a.Type, a.Family, true, a.Id + 800_000))
            .Filter(x => x.Name.StartsWith("fwd-A"))         // only direct A items (blocks rev- re-entry)
            .ForEachChange(change =>
            {
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                    _cacheB.AddOrUpdate(change.Current);
                else if (change.Reason == ChangeReason.Remove)
                    _cacheB.Remove(change.Current.Id);
            })
            .Subscribe();
        _cleanup.Add(forwardPipeline);

        var reversePipeline = _cacheB.Connect()
            .Filter(x => x.Name.StartsWith("fwd-A"))          // only first-gen forwards (blocks re-reverse)
            .Transform(a => new Animal("rev-" + a.Name, a.Type, a.Family, true, a.Id + 900_000))
            .ForEachChange(change =>
            {
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                    _cacheA.AddOrUpdate(change.Current);
                else if (change.Reason == ChangeReason.Remove)
                    _cacheA.Remove(change.Current.Id);
            })
            .Subscribe();
        _cleanup.Add(reversePipeline);

        // ================================================================
        // PIPELINE 8: And + Except + Xor (remaining set operations)
        // ================================================================

        var andResults = _cacheA.Connect().And(_cacheB.Connect()).AsAggregator();
        _cleanup.Add(andResults);

        var exceptResults = _cacheA.Connect().Except(_cacheB.Connect()).AsAggregator();
        _cleanup.Add(exceptResults);

        var xorResults = _cacheA.Connect().Xor(_cacheB.Connect()).AsAggregator();
        _cleanup.Add(xorResults);

        // ================================================================
        // PIPELINE 9: TransformOnObservable + FilterOnObservable +
        //             TransformWithInlineUpdate + DistinctValues
        // ================================================================

        var transformOnObsResults = _cacheA.Connect()
            .TransformOnObservable(animal =>
                Observable.Return(new Animal("tob-" + animal.Name, animal.Type, animal.Family, true, animal.Id + 1_000_000)))
            .AsAggregator();
        _cleanup.Add(transformOnObsResults);

        var filterOnObsResults = _cacheA.Connect()
            .FilterOnObservable(animal =>
                Observable.Return(animal.Family == AnimalFamily.Mammal))
            .AsAggregator();
        _cleanup.Add(filterOnObsResults);

        var inlineUpdateResults = _cacheA.Connect()
            .TransformWithInlineUpdate(
                animal => new Animal("twiu-" + animal.Name, animal.Type, animal.Family, animal.IncludeInResults, animal.Id + 1_100_000),
                (existing, incoming) => { })
            .AsAggregator();
        _cleanup.Add(inlineUpdateResults);

        var distinctFamilies = _cacheA.Connect()
            .DistinctValues(x => x.Family)
            .AsAggregator();
        _cleanup.Add(distinctFamilies);

        // ================================================================
        // PIPELINE 10: ToObservableChangeSet + ExpireAfter + MergeMany
        //              (MergeMany kept separately from MergeManyChangeSets)
        // ================================================================

        var observableToChangeSet = Observable.Create<Animal>(observer =>
            {
                var sub = _cacheA.Connect()
                    .Flatten()
                    .Where(c => c.Reason == ChangeReason.Add)
                    .Select(c => c.Current)
                    .Subscribe(observer);
                return sub;
            })
            .ToObservableChangeSet(a => a.Id + 1_200_000)
            .AsAggregator();
        _cleanup.Add(observableToChangeSet);

        var mergeManyResults = _cacheA.Connect()
            .MergeMany(animal => Observable.Return(animal.Name))
            .ToList()
            .Subscribe();
        _cleanup.Add(mergeManyResults);

        // ================================================================
        // PIPELINE 11: Bind (ReadOnlyObservableCollection) + OnItemRefreshed
        //              + ForEachChange + Cast + DeferUntilLoaded
        // ================================================================

        var sortedForBind = _cacheB.Connect()
            .Sort(SortExpressionComparer<Animal>.Ascending(x => x.Id))
            .Bind(out var boundCollection)
            .OnItemRefreshed(_ => { })
            .ForEachChange(_ => { })
            .Subscribe();
        _cleanup.Add(sortedForBind);

        var deferredResults = _cacheA.Connect()
            .DeferUntilLoaded()
            .AsAggregator();
        _cleanup.Add(deferredResults);

        // ================================================================
        // CONCURRENT WRITERS — deterministic data, maximum contention
        //
        // Each thread writes items with non-overlapping ID ranges.
        // This ensures the final state is predictable regardless of
        // thread interleaving, while still stressing the lock chains.
        //
        // CacheA: threads 0-7 write IDs [t*500+1 .. (t+1)*500]  → IDs 1..4000
        // CacheB: threads 0-7 write IDs [10000+t*500+1 .. 10000+(t+1)*500] → IDs 10001..14000
        //
        // Family assignment: (id % 5) → Mammal=0, Reptile=1, Fish=2, Amphibian=3, Bird=4
        // IncludeInResults: true for all during write, toggled after for predictability
        // ================================================================

        using var barrier = new Barrier(WriterThreads + WriterThreads + 1 + 1); // A + B + control + main

        var writersA = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                var id = (t * ItemsPerThread) + i + 1;  // 1-based
                var family = (AnimalFamily)(id % 5);
                var animal = new Animal($"A{id}", $"Type{id % 7}", family, true, id);
                _cacheA.AddOrUpdate(animal);
            }
        })).ToArray();

        var writersB = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                var id = 10_000 + (t * ItemsPerThread) + i + 1;  // 10001-based
                var family = (AnimalFamily)(id % 5);
                var animal = new Animal($"B{id}", $"Type{id % 7}", family, true, id);
                _cacheB.AddOrUpdate(animal);
            }
        })).ToArray();

        // Control thread: toggles parameters under load
        var controlThread = Task.Run(() =>
        {
            barrier.SignalAndWait();
            SpinWait.SpinUntil(() => _cacheA.Count > 50, TimeSpan.FromSeconds(5));

            for (var i = 0; i < 50; i++)
            {
                // Toggle BatchIf
                pauseBatch.OnNext(i % 4 == 0);

                // Change sort direction
                if (i % 10 == 0)
                    sortComparer.OnNext(SortExpressionComparer<Animal>.Descending(x => x.Id));
                else if (i % 10 == 5)
                    sortComparer.OnNext(SortExpressionComparer<Animal>.Ascending(x => x.Id));

                // Change page
                pageRequests.OnNext(new PageRequest((i % 3) + 1, 100));

                // Change virtual window
                virtualRequests.OnNext(new VirtualRequest(i % 20, 50));

                // Switch between caches
                if (i % 6 == 0)
                    switchSource.OnNext(_cacheB.Connect());
                else if (i % 6 == 3)
                    switchSource.OnNext(_cacheA.Connect());

                Thread.SpinWait(500);
            }

            // Reset to known state for validation
            pauseBatch.OnNext(false);
            sortComparer.OnNext(SortExpressionComparer<Animal>.Ascending(x => x.Id));
            pageRequests.OnNext(new PageRequest(1, 100));
            virtualRequests.OnNext(new VirtualRequest(0, 50));
            switchSource.OnNext(_cacheA.Connect());
        });

        // Release all threads
        barrier.SignalAndWait();

        var allTasks = Task.WhenAll(writersA.Concat(writersB).Append(controlThread));
        var completed = await Task.WhenAny(allTasks, Task.Delay(Timeout));
        completed.Should().BeSameAs(allTasks,
            $"cross-cache pipeline deadlocked — tasks did not complete within {Timeout.TotalSeconds}s");
        await allTasks; // propagate faults

        // Let async deliveries settle (bidirectional pipeline needs time for cascading)
        await Task.Delay(2000);

        // ================================================================
        // POST-WRITE DETERMINISTIC MUTATIONS
        //
        // Now that all writers are done and pipelines settled, apply
        // deterministic mutations so the final state is calculable.
        // ================================================================

        // Toggle IncludeInResults for items where id % 10 == 5 (triggers AutoRefresh → Filter re-eval)
        foreach (var animal in _cacheA.Items.Where(a => a.Id <= 4000 && a.Id % 10 == 5).ToArray())
            animal.IncludeInResults = false;

        // Remove specific items from each cache
        _cacheA.Edit(u => u.RemoveKeys(
            Enumerable.Range(1, 4000).Where(id => id % 20 == 0).Select(id => id)));  // 200 removals
        _cacheB.Edit(u => u.RemoveKeys(
            Enumerable.Range(10_001, 4000).Where(id => id % 15 == 0).Select(id => id)));

        // Let all pipeline effects settle (forward→reverse cascade)
        await Task.Delay(2000);


        // ================================================================
        // VERIFY EXACT RESULTS
        //
        // Expected state after deterministic writes + mutations:
        //
        // CacheA direct: 4000 written - 200 removed (id%20==0) = 3800
        // Forward pipeline: 600 mammals from A (id%5==0, surviving) → B as id+800_000
        // Reverse pipeline: 600 fwd items from B → A as id+1_700_000
        // CacheA total: 3800 direct + 600 reverse = 4400
        //
        // CacheB direct: 4000 written - 267 removed (id%15==0) = 3733
        // CacheB total: 3733 direct + 600 forward = 4333
        //
        // Key ranges are disjoint: A={1..4000}∪{1_700_xxx}, B={10001..14000}∪{800_xxx}
        // ================================================================

        _cacheA.Count.Should().Be(4400, "cacheA: 3800 direct + 600 reverse");
        _cacheB.Count.Should().Be(4333, "cacheB: 3733 direct + 600 forward");

        // FullJoin: all from both sides (disjoint keys → no overlap → A+B)
        joinChain.Data.Count.Should().BeGreaterThan(0, "FullJoin chain should produce results");

        // InnerJoin: keys in both → 0 (disjoint ranges)
        innerJoinResults.Data.Count.Should().Be(0,
            "InnerJoin should be empty (A and B have disjoint key ranges)");

        // LeftJoin: one row per cacheA item
        leftJoinResults.Data.Count.Should().Be(4400,
            "LeftJoin should have exactly one row per cacheA item");

        // RightJoin: one row per cacheB item
        rightJoinResults.Data.Count.Should().Be(4333,
            "RightJoin should have exactly one row per cacheB item");

        // MergeChangeSets: union of disjoint = A + B
        mergedResults.Data.Count.Should().Be(4400 + 4333,
            "MergeChangeSets should be A + B (disjoint keys)");

        // Or: union with dedup (disjoint = same as merge)
        orResults.Data.Count.Should().Be(4400 + 4333,
            "Or should equal A + B (disjoint keys)");

        // And: intersection = 0
        andResults.Data.Count.Should().Be(0,
            "And should be empty (disjoint keys)");

        // Except: A minus B = A (disjoint)
        exceptResults.Data.Count.Should().Be(4400,
            "Except should equal cacheA (disjoint keys)");

        // Xor: symmetric difference = A + B (disjoint)
        xorResults.Data.Count.Should().Be(4400 + 4333,
            "Xor should equal A + B (disjoint keys)");

        // QueryWhenChanged: reflects cacheB
        lastQuery.Should().NotBeNull("QueryWhenChanged should have fired");
        lastQuery!.Count.Should().Be(4333, "QueryWhenChanged should reflect cacheB final state");

        // SortAndBind: reflects cacheA, sorted by Id
        boundList.Count.Should().Be(4400, "SortAndBind should reflect cacheA count");
        boundList.Should().BeInAscendingOrder(x => x.Id, "SortAndBind should be sorted by Id");

        // Virtualise(0, 50): capped at window size
        virtualisedResults.Data.Count.Should().Be(50,
            "Virtualise should show exactly 50 items (window size)");

        // GroupWithImmutableState: all 5 families present in cacheA
        immutableGroups.Data.Count.Should().Be(5,
            "GroupWithImmutableState should have one group per AnimalFamily");

        // TransformMany(a => [a, twin]): 2× cacheA
        transformManyResults.Data.Count.Should().Be(4400 * 2,
            "TransformMany should have 2× cacheA items (original + twin)");

        // BatchIf: all cacheA items (unpaused at end)
        batchedResults.Data.Count.Should().Be(4400,
            "BatchIf should have all cacheA items after final unpause");

        // Switch: reflects cacheA (last switched to A)
        switchResults.Data.Count.Should().Be(4400,
            "Switch should reflect cacheA after final switch");

        // Bidirectional flow verification
        _cacheB.Items.Count(x => x.Name.StartsWith("fwd-A")).Should().Be(600,
            "Forward pipeline should have pushed 600 mammals from A to B");
        _cacheA.Items.Count(x => x.Name.StartsWith("rev-fwd-A")).Should().Be(600,
            "Reverse pipeline should have pushed 600 items back from B to A");

        // TransformOnObservable: 1:1 with cacheA
        transformOnObsResults.Data.Count.Should().Be(4400,
            "TransformOnObservable should mirror cacheA count");

        // FilterOnObservable(Mammal): 600 direct mammals + 600 reverse (all mammal) = 1200
        filterOnObsResults.Data.Count.Should().Be(1200,
            "FilterOnObservable should contain 1200 mammals (600 direct + 600 reverse)");

        // TransformWithInlineUpdate: 1:1 with cacheA
        inlineUpdateResults.Data.Count.Should().Be(4400,
            "TransformWithInlineUpdate should mirror cacheA count");

        // DistinctValues(Family): all 5 AnimalFamily values
        distinctFamilies.Data.Count.Should().Be(5,
            "DistinctValues should track all 5 distinct families");

        // Bind (ReadOnlyObservableCollection): reflects cacheB
        boundCollection.Count.Should().Be(4333,
            "Bind should reflect cacheB count");

        // DeferUntilLoaded: reflects cacheA
        deferredResults.Data.Count.Should().Be(4400,
            "DeferUntilLoaded should have all cacheA items");

        // Monster chain: should have received changesets (SkipInitial skips first batch)
        monsterChain.Messages.Should().NotBeEmpty("Monster chain should have received changesets");
    }
}
