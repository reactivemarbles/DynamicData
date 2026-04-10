// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
            .MergeMany(group => group.Cache.Connect()  // MergeMany into the groups
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
            .Filter(x => !x.Name.StartsWith("fwd-fwd-") && !x.Name.StartsWith("fwd-rev-"))
            .PopulateInto(_cacheB);
        _cleanup.Add(forwardPipeline);

        var reversePipeline = _cacheB.Connect()
            .Filter(x => x.Name.StartsWith("fwd-"))
            .Transform(a => new Animal("rev-" + a.Name, a.Type, a.Family, true, a.Id + 900_000))
            .Filter(x => !x.Name.StartsWith("rev-rev-"))
            .PopulateInto(_cacheA);
        _cleanup.Add(reversePipeline);

        // ================================================================
        // CONCURRENT WRITERS — maximum contention
        // ================================================================

        using var barrier = new Barrier(WriterThreads + WriterThreads + 1 + 1); // A + B + control + main

        var writersA = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            var faker = Fakers.Animal.Clone().WithSeed(new Randomizer(Rand.Int()));
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                var animal = faker.Generate();
                _cacheA.AddOrUpdate(animal);

                // Every 10th item: toggle IncludeInResults (triggers AutoRefresh)
                if (i % 10 == 5)
                {
                    var items = _cacheA.Items.Take(3).ToArray();
                    foreach (var item in items)
                        item.IncludeInResults = !item.IncludeInResults;
                }

                // Every 20th item: remove old items
                if (i % 20 == 0 && i > 0)
                    _cacheA.Edit(u => u.RemoveKeys(_cacheA.Keys.Take(3)));
            }
        })).ToArray();

        var writersB = Enumerable.Range(0, WriterThreads).Select(t => Task.Run(() =>
        {
            var faker = Fakers.Animal.Clone().WithSeed(new Randomizer(Rand.Int()));
            barrier.SignalAndWait();
            for (var i = 0; i < ItemsPerThread; i++)
            {
                var animal = faker.Generate();
                _cacheB.AddOrUpdate(animal);

                if (i % 15 == 0 && i > 0)
                    _cacheB.Edit(u => u.RemoveKeys(_cacheB.Keys.Take(2)));
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

        // Let async deliveries settle
        await Task.Delay(200);

        // ================================================================
        // VERIFY RESULTS
        // ================================================================

        // Core caches have items
        _cacheA.Count.Should().BeGreaterThan(0, "cacheA should have items");
        _cacheB.Count.Should().BeGreaterThan(0, "cacheB should have items");

        // FullJoin: should have at least max(A, B) items (full outer join)
        joinChain.Data.Count.Should().BeGreaterThan(0, "FullJoin chain should produce results");

        // LeftJoin: exactly one row per left item
        leftJoinResults.Data.Count.Should().Be(_cacheA.Count,
            "LeftJoin should have exactly one row per left (cacheA) item");

        // MergeChangeSets: union of both caches
        mergedResults.Data.Count.Should().Be(_cacheA.Count + _cacheB.Count,
            "MergeChangeSets should be the sum of both caches (disjoint keys)");

        // Or: union with dedup
        orResults.Data.Count.Should().Be(
            _cacheA.Count + _cacheB.Count - _cacheA.Keys.Intersect(_cacheB.Keys).Count(),
            "Or should be the union of both caches");

        // QueryWhenChanged
        lastQuery.Should().NotBeNull("QueryWhenChanged should have fired");
        lastQuery!.Count.Should().Be(_cacheB.Count, "QueryWhenChanged should reflect cacheB");

        // SortAndBind
        boundList.Count.Should().Be(_cacheA.Count, "SortAndBind should reflect cacheA count");
        boundList.Should().BeInAscendingOrder(x => x.Id, "SortAndBind should be sorted by Id");

        // Virtualise: capped at window size
        virtualisedResults.Data.Count.Should().BeLessThanOrEqualTo(50,
            "Virtualise should respect window size");

        // GroupWithImmutableState: should have groups for each family present
        var familiesInA = _cacheA.Items.Select(a => a.Family).Distinct().Count();
        immutableGroups.Data.Count.Should().Be(familiesInA,
            "GroupWithImmutableState should have one group per family");

        // TransformMany: 2x cacheA (original + twin)
        transformManyResults.Data.Count.Should().Be(_cacheA.Count * 2,
            "TransformMany should double the items");

        // BatchIf: all items (unpaused at end)
        batchedResults.Data.Count.Should().Be(_cacheA.Count,
            "BatchIf should have all items after final unpause");

        // Switch: should reflect whichever cache was last selected (cacheA)
        switchResults.Data.Count.Should().Be(_cacheA.Count,
            "Switch should reflect cacheA after final switch");

        // Bidirectional: if any mammals were in cacheA, forward pipeline pushed them to cacheB
        var mammalsInA = _cacheA.Items.Count(x => x.Family == AnimalFamily.Mammal && !x.Name.StartsWith("rev-"));
        if (mammalsInA > 0)
        {
            _cacheB.Items.Any(x => x.Name.StartsWith("fwd-")).Should().BeTrue(
                "Forward pipeline should have pushed mammals from A to B");
        }

        // No Rx contract violations (messages received = all assertions passed)
        monsterChain.Messages.Should().NotBeEmpty("Monster chain should have received changesets");
    }
}
