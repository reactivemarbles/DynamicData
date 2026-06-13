// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Concurrency and event-delivery tests for <see cref="NotifyPropertyChangedEx.WhenPropertyChanged{TObject, TProperty}"/>
/// and <see cref="NotifyPropertyChangedEx.WhenValueChanged{TObject, TProperty}"/>.
/// </summary>
/// <remarks>
/// Verifies that every <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> emission reaches the
/// downstream observer, including events that fire while the operator's subscribe call is in flight, events on deep
/// chains with concurrent mutation at multiple levels, and same-valued events that follow the initial emission.
/// </remarks>
public sealed class WhenPropertyChangedRaceFixture
{
    // CI-friendly: tests should complete in ms but allow generous budget on heavily-loaded shared runners.
    private static readonly TimeSpan DefaultConditionTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Shallow_ConcurrentMutationDuringInitialEmit_NotDropped()
    {
        var model = new TestModel { Value = 10 };
        var emissions = new List<int>();
        var observerInitialReceived = new ManualResetEventSlim(false);
        var observerCanContinue = new ManualResetEventSlim(false);

        var observer = Observer.Create<PropertyValue<TestModel, int>>(pv =>
        {
            bool isFirst;
            lock (emissions)
            {
                isFirst = emissions.Count == 0;
                emissions.Add(pv.Value);
            }

            if (isFirst)
            {
                // Hold the OnNext open. Any PropertyChanged event the test thread fires while the
                // observer is blocked here must still reach the observer once it returns.
                observerInitialReceived.Set();
                observerCanContinue.Wait(DefaultConditionTimeout);
            }
        });

        // Subscribe on a worker thread so the test thread can mutate and release while Subscribe is blocked.
        var subscribeTask = Task.Run(() =>
            model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: true).Subscribe(observer));

        try
        {
            observerInitialReceived.Wait(DefaultConditionTimeout).Should().BeTrue("the worker thread must reach the initial emission");

            // Mutate while Subscribe is parked inside observer.OnNext for the initial value.
            model.Value = 20;
        }
        finally
        {
            observerCanContinue.Set();
        }

        subscribeTask.Wait(DefaultConditionTimeout).Should().BeTrue("Subscribe must complete");
        using var sub = subscribeTask.Result;

        WaitForCondition(() => { lock (emissions) return emissions.Contains(20); });

        lock (emissions)
        {
            emissions.Should().Contain(20,
                "a PropertyChanged notification fired during the subscribe gap must not be dropped");
        }
    }

    [Fact]
    public void Shallow_NotifyInitialFalse_StillSubscribesEventBeforeReturning()
    {
        var model = new TestModel { Value = 10 };
        var emissions = new List<int>();

        // Subscribe on this thread; with notifyOnInitialValue: false, Subscribe should not block.
        using var sub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
            .Subscribe(pv =>
            {
                lock (emissions) emissions.Add(pv.Value);
            });

        // The act of returning from Subscribe must mean the event handler is attached.
        model.Value = 20;

        WaitForCondition(() => { lock (emissions) return emissions.Count >= 1; });

        lock (emissions)
        {
            emissions.Should().Equal(new[] { 20 }, "no initial value requested; first emission must be the mutation");
        }
    }

    [Fact]
    public void PropertyChangedEventsAreNeverDropped_RegardlessOfNotifyInitial()
    {
        // Every PropertyChanged event must reach the observer, even when its value equals the
        // most recently observed value. The same-valued case is the diagnostic: any equality
        // dedup would silently drop one of these emissions and the test would see fewer values
        // than were written. Covers both notifyOnInitialValue settings and both shallow and
        // deep chains.
        //
        // Expected emissions per scenario:
        //   Shallow + notifyInitial=true:  initial(10) + setter(10)*3 = 4 emissions.
        //   Shallow + notifyInitial=false: setter(42)*2                = 2 emissions.
        //   Deep    + notifyInitial=true:  initial(1)  + setter(7)*3   = 4 emissions.
        //   Deep    + notifyInitial=false: setter(7)*2                 = 2 emissions.

        // Shallow, notifyInitial=true: initial value matches every subsequent setter.
        var modelT = new TestModel { Value = 10 };
        var shallowTrueEmissions = new List<int>();
        using (var sub = modelT.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: true)
                   .Subscribe(pv => { lock (shallowTrueEmissions) shallowTrueEmissions.Add(pv.Value); }))
        {
            modelT.Value = 10;
            modelT.Value = 10;
            modelT.Value = 10;
            WaitForCondition(() => { lock (shallowTrueEmissions) return shallowTrueEmissions.Count >= 4; });
        }
        lock (shallowTrueEmissions)
        {
            shallowTrueEmissions.Should().Equal(new[] { 10, 10, 10, 10 },
                "shallow notifyInitial=true must deliver the initial plus every same-valued setter without dedup");
        }

        // Shallow, notifyInitial=false: no initial, every setter delivered.
        var modelF = new TestModel { Value = 10 };
        var shallowFalseEmissions = new List<int>();
        using (var sub = modelF.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
                   .Subscribe(pv => { lock (shallowFalseEmissions) shallowFalseEmissions.Add(pv.Value); }))
        {
            modelF.Value = 42;
            modelF.Value = 42;
            WaitForCondition(() => { lock (shallowFalseEmissions) return shallowFalseEmissions.Count >= 2; });
        }
        lock (shallowFalseEmissions)
        {
            shallowFalseEmissions.Should().Equal(new[] { 42, 42 },
                "shallow notifyInitial=false must deliver both same-valued setters");
        }

        // Deep, notifyInitial=true: initial leaf value matches every subsequent setter.
        var parentT = new ParentModel { Child = new ChildModel { Age = 1 } };
        var deepTrueEmissions = new List<int>();
        using (var sub = parentT.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true)
                   .Subscribe(pv => { lock (deepTrueEmissions) deepTrueEmissions.Add(pv.Value); }))
        {
            parentT.Child!.Age = 1;
            parentT.Child!.Age = 1;
            parentT.Child!.Age = 1;
            WaitForCondition(() => { lock (deepTrueEmissions) return deepTrueEmissions.Count >= 4; });
        }
        lock (deepTrueEmissions)
        {
            deepTrueEmissions.Should().Equal(new[] { 1, 1, 1, 1 },
                "deep notifyInitial=true must deliver the initial plus every same-valued setter without dedup");
        }

        // Deep, notifyInitial=false: no initial, every setter delivered.
        var parentF = new ParentModel { Child = new ChildModel { Age = 1 } };
        var deepFalseEmissions = new List<int>();
        using (var sub = parentF.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
                   .Subscribe(pv => { lock (deepFalseEmissions) deepFalseEmissions.Add(pv.Value); }))
        {
            parentF.Child!.Age = 7;
            parentF.Child!.Age = 7;
            WaitForCondition(() => { lock (deepFalseEmissions) return deepFalseEmissions.Count >= 2; });
        }
        lock (deepFalseEmissions)
        {
            deepFalseEmissions.Should().Equal(new[] { 7, 7 },
                "deep notifyInitial=false must deliver both same-valued setters");
        }
    }

    [Fact]
    public void DeepChain_PostSwap_LeafEventOnNewChild_Captured()
    {
        // After parent.Child is reassigned to a new instance, the leaf-level subscription must be
        // re-attached against the new child. A subsequent leaf mutation on the new child must be
        // captured. Verifies the chain re-walk behavior end-to-end.
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };
        var emissions = new List<int>();

        using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true)
            .Subscribe(pv =>
            {
                lock (emissions) emissions.Add(pv.Value);
            });

        var newChild = new ChildModel { Age = 20 };
        parent.Child = newChild;
        newChild.Age = 30;

        WaitForCondition(() => { lock (emissions) return emissions.Contains(30); });

        lock (emissions)
        {
            emissions.Should().Contain(10, "initial value before swap");
            emissions.Should().Contain(20, "post-swap initial leaf value of new child");
            emissions.Should().Contain(30, "leaf mutation on new child after swap must be captured");
        }
    }

    [Fact]
    public void DeepChain_ConcurrentLeafMutationDuringInitialEmit_NotDropped()
    {
        // The worker thread blocks inside the initial-emit OnNext while the test thread mutates
        // the leaf. SharedDeliveryQueue enqueues the leaf signal onto the signal sub-queue and
        // returns immediately; when the observer unblocks, the drainer processes the signal and
        // delivers the resulting value.
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };
        var emissions = new List<int>();
        var observerInitialReceived = new ManualResetEventSlim(false);
        var observerCanContinue = new ManualResetEventSlim(false);

        var observer = Observer.Create<PropertyValue<ParentModel, int>>(pv =>
        {
            bool isFirst;
            lock (emissions)
            {
                isFirst = emissions.Count == 0;
                emissions.Add(pv.Value);
            }

            if (isFirst)
            {
                observerInitialReceived.Set();
                observerCanContinue.Wait(DefaultConditionTimeout);
            }
        });

        var subscribeTask = Task.Run(() =>
            parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: true).Subscribe(observer));

        try
        {
            observerInitialReceived.Wait(DefaultConditionTimeout).Should().BeTrue("the worker thread must reach the initial emission");

            parent.Child!.Age = 20;
        }
        finally
        {
            observerCanContinue.Set();
        }

        subscribeTask.Wait(DefaultConditionTimeout).Should().BeTrue("Subscribe must complete");
        using var sub = subscribeTask.Result;

        WaitForCondition(() => { lock (emissions) return emissions.Contains(20); });

        lock (emissions)
        {
            emissions.Should().Contain(20, "a leaf PropertyChanged fired during the subscribe gap must not be dropped");
        }
    }

    [Fact]
    public void DeepChain_ConcurrentParentSwap_LeafEventOnWinnerNotDropped()
    {
        // Two threads concurrently swap parent.Child. SharedDeliveryQueue serializes the level-0
        // signals on the drainer, so ResubscribeFrom runs serially and the final level-1
        // subscription always targets parent.Child's current (latest) value. A leaf mutation on
        // the winning child must always be captured.
        //
        // The iteration count is defence in depth: one iteration is sufficient because the drainer
        // serialization makes the outcome deterministic.
        const int iterations = 50;
        var losses = 0;

        for (var i = 0; i < iterations; i++)
        {
            var parent = new ParentModel { Child = new ChildModel { Age = 0 } };
            var emissions = new List<int>();

            using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
                .Subscribe(pv =>
                {
                    lock (emissions) emissions.Add(pv.Value);
                });

            var newChild1 = new ChildModel { Age = 1 };
            var newChild2 = new ChildModel { Age = 2 };

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); parent.Child = newChild1; });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); parent.Child = newChild2; });

            // Task.WaitAll only returns once both tasks have left Subscribe; the drainer (whichever
            // task became it) has drained both queued signals before returning, so the leaf
            // subscription is correctly attached to whichever child is parent.Child by now.
            Task.WaitAll([taskA, taskB], DefaultConditionTimeout).Should().BeTrue();

            var winner = parent.Child;
            if (winner is null)
            {
                continue;
            }

            winner.Age = 99;

            WaitForCondition(() => { lock (emissions) return emissions.Contains(99); });

            lock (emissions)
            {
                if (!emissions.Contains(99))
                {
                    losses++;
                }
            }
        }

        losses.Should().Be(0,
            $"out of {iterations} iterations, {losses} dropped the leaf event on the post-swap winner");
    }

    [Fact]
    public void DeepChain_FiveLevels_MidChainSwap_DeeperLevelsRetargetCorrectly()
    {
        // Verifies the per-level re-attach logic on a 5-level chain. When a mid-chain swap
        // happens (the 3rd level out of 5 is reassigned), levels 4 and 5 must be re-attached
        // against the new value's subtree. Subsequent leaf events through the new chain must be
        // captured; events on the OLD subtree must be ignored (its subscriptions are disposed).
        var l1 = new Level1
        {
            Child = new Level2
            {
                Child = new Level3
                {
                    Child = new Level4
                    {
                        Leaf = 10,
                    },
                },
            },
        };

        var emissions = new List<int>();
        using var sub = l1.WhenPropertyChanged(x => x.Child!.Child!.Child!.Leaf, notifyOnInitialValue: true)
            .Subscribe(pv =>
            {
                lock (emissions) emissions.Add(pv.Value);
            });

        lock (emissions) emissions.Should().Equal(new[] { 10 }, "initial emission");

        // Capture the original leaf for stale-event verification.
        var originalLeaf = l1.Child!.Child!.Child!;

        // Swap level 3 (l1.Child.Child.Child = new Level4 { Leaf = 20 }). This fires the notifier
        // at level 3, which should cause level 4 (the leaf notifier) to be re-attached on the new
        // Level4 instance.
        var newL4 = new Level4 { Leaf = 20 };
        l1.Child!.Child!.Child = newL4;

        lock (emissions)
        {
            emissions.Should().HaveCount(2);
            emissions[1].Should().Be(20, "swap of mid-level should emit the new leaf value");
        }

        // Mutate the leaf on the NEW subtree. Should be captured.
        newL4.Leaf = 30;
        lock (emissions)
        {
            emissions.Should().HaveCount(3);
            emissions[2].Should().Be(30, "leaf event on new subtree must be captured");
        }

        // Mutate the leaf on the OLD subtree (should be ignored; its subscription was disposed).
        originalLeaf.Leaf = 999;
        lock (emissions)
        {
            emissions.Should().HaveCount(3, "leaf event on disposed old subtree must NOT be emitted");
        }
    }

    private sealed class Level1 : INotifyPropertyChanged
    {
        private Level2? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level2? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level2 : INotifyPropertyChanged
    {
        private Level3? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level3? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level3 : INotifyPropertyChanged
    {
        private Level4? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level4? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level4 : INotifyPropertyChanged
    {
        private int _leaf;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Leaf
        {
            get => _leaf;
            set
            {
                _leaf = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }

    private sealed class TestModel : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class ParentModel : INotifyPropertyChanged
    {
        private ChildModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChildModel? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class ChildModel : INotifyPropertyChanged
    {
        private int _age;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
            }
        }
    }

    [Fact]
    public async Task DeepChain_FiveLevels_AllLevelsMutatedConcurrently_FinalEmissionMatchesActual()
    {
        // Torture: a 5-level chain with FIVE worker threads each mutating at one level.
        //   - Thread 0 swaps the entire subtree at root.Child (level 0)
        //   - Thread 1 swaps the subtree at root.Child.Child (level 1)
        //   - Thread 2 swaps the subtree at level 2
        //   - Thread 3 swaps the Deep5 instance at level 3
        //   - Thread 4 mutates the int Leaf on the current Deep5 (level 4)
        //
        // Many mutations land on detached objects (their subtree was swapped out by a higher-level
        // mutation between the local-variable read and the property set). Those events are
        // CORRECTLY ignored: their notifier subscription was disposed when ResubscribeFrom replaced
        // the SerialDisposable at that level. Mutations on the live chain reach the drainer.
        //
        // Three invariants verified per iteration:
        //   (a) Rx contract: ValidateSynchronization on the subscription chain catches any
        //       concurrent OnNext (a SharedDeliveryQueue serialization failure).
        //   (b) Value legality: every emission must be a value that some thread legitimately
        //       wrote (catches torn reads or stale values from detached subtrees being mis-read).
        //   (c) Final consistency: after Task.WhenAll the drainer continues until the queue is
        //       empty. The LAST processed signal triggered a ReadCurrent against whatever the
        //       chain looked like at that moment; since no further mutations happen after
        //       Task.WhenAll, that moment's state equals the chain state right now. Therefore
        //       emissions.Last() == ReadCurrent().
        //
        // What this test does NOT verify: that every mutation which landed on the live chain
        // produced an emission. That requires causal-history reconstruction which isn't tractable
        // from outside the operator.
        const int iterations = 50;
        const int mutationsPerThread = 200;
        var mismatches = 0;

        for (var iter = 0; iter < iterations; iter++)
        {
            var root = NewDeepChain(0);
            var emissions = new List<int>();

            using var sub = root.WhenPropertyChanged(r => r.Child!.Child!.Child!.Child!.Leaf, notifyOnInitialValue: true)
                .ValidateSynchronization()
                .Subscribe(pv => { lock (emissions) emissions.Add(pv.Value); });

            using var barrier = new Barrier(5);
            var iterSeed = iter * 10_000;
            var tasks = new[]
            {
                Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < mutationsPerThread; i++)
                    {
                        root.Child = NewDeep2(iterSeed + 40_000 + i);
                    }
                }),
                Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < mutationsPerThread; i++)
                    {
                        var l2 = root.Child;
                        if (l2 is not null) l2.Child = NewDeep3(iterSeed + 30_000 + i);
                    }
                }),
                Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < mutationsPerThread; i++)
                    {
                        var l3 = root.Child?.Child;
                        if (l3 is not null) l3.Child = NewDeep4(iterSeed + 20_000 + i);
                    }
                }),
                Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < mutationsPerThread; i++)
                    {
                        var l4 = root.Child?.Child?.Child;
                        if (l4 is not null) l4.Child = new Deep5 { Leaf = iterSeed + 10_000 + i };
                    }
                }),
                Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < mutationsPerThread; i++)
                    {
                        var l5 = root.Child?.Child?.Child?.Child;
                        if (l5 is not null) l5.Leaf = i;
                    }
                }),
            };

            await Task.WhenAll(tasks).WaitAsync(DefaultConditionTimeout);

            var actualFinal = root.Child!.Child!.Child!.Child!.Leaf;

            WaitForCondition(() => { lock (emissions) return emissions.Count > 0 && emissions[^1] == actualFinal; });

            // Build the set of values any thread could legitimately have written.
            var legal = new HashSet<int> { 0 }; // initial leaf
            for (var i = 0; i < mutationsPerThread; i++)
            {
                legal.Add(i);                              // thread 4 (leaf int)
                legal.Add(iterSeed + 10_000 + i);          // thread 3 (Deep5 swap)
                legal.Add(iterSeed + 20_000 + i);          // thread 2 (Deep4 subtree)
                legal.Add(iterSeed + 30_000 + i);          // thread 1 (Deep3 subtree)
                legal.Add(iterSeed + 40_000 + i);          // thread 0 (Deep2 subtree)
            }

            lock (emissions)
            {
                emissions.Should().NotBeEmpty($"iter {iter}: notifyOnInitialValue=true requires at least the initial emission");
                emissions[0].Should().Be(0, $"iter {iter}: first emission must be the initial value");

                var illegal = emissions.Where(v => !legal.Contains(v)).ToList();
                illegal.Should().BeEmpty($"iter {iter}: every emission must be a value some thread wrote; saw {string.Join(",", illegal.Take(5))}");

                if (emissions.Count == 0 || emissions[^1] != actualFinal)
                {
                    mismatches++;
                }
            }
        }

        mismatches.Should().Be(0,
            $"out of {iterations} iterations, {mismatches} ended with the last emission not matching the actual final chain leaf");
    }

    [Fact]
    public async Task AutoRefreshThenFilter_ConcurrentPropertyMutationsOnAddedItems_AllFinalStatesObserved()
    {
        // Integration: SourceCache + AutoRefresh + Filter under concurrent post-add property
        // mutation. The cache is pre-populated (Activated=false), and once AutoRefresh has fully
        // subscribed per-item, multiple worker threads concurrently mutate Activated. After the
        // storm, every item that ends Activated=true must appear in the filter and every item
        // that ends Activated=false must not.
        //
        // Exercises WhenPropertyChanged under multi-threaded contention: many concurrent
        // PropertyChanged invocations per item, all wrapped through SinglePropertySubscription's
        // DeliveryQueue so the Rx contract is preserved end-to-end on the per-item path.
        //
        // Adds are NOT interleaved with property mutations. ObservableCache.CreateConnectObservable
        // uses an initial.Concat(_changes) shape whose subscribe-window race can drop a
        // concurrently-fired change before the AutoRefresh subscriber wires up; that is a
        // cache-side concern and not what this test is targeting. Pre-populating ensures the
        // signals observed here are produced solely by the per-item WhenPropertyChanged path.
        const int iterations = 30;
        const int itemCount = 200;
        const int mutatorThreads = 4;

        var randomizer = new Random(Seed: 1234);

        for (var iter = 0; iter < iterations; iter++)
        {
            using var cache = new SourceCache<KeyedActivable, int>(x => x.Id);
            var items = Enumerable.Range(0, itemCount).Select(i => new KeyedActivable(i)).ToList();

            // Each item gets a final Activated value determined up front. All mutator threads
            // write the SAME final value many times, so the eventual state is deterministic.
            var finalActive = items.ToDictionary(x => x.Id, _ => randomizer.Next(2) == 1);

            foreach (var item in items) cache.AddOrUpdate(item);

            using var results = cache.Connect()
                .AutoRefresh(x => x.Activated)
                .Filter(x => x.Activated)
                .AsAggregator();

            using var barrier = new Barrier(mutatorThreads);
            var mutators = Enumerable.Range(0, mutatorThreads)
                .Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (var pass = 0; pass < 3; pass++)
                    {
                        foreach (var item in items)
                        {
                            item.Activated = finalActive[item.Id];
                        }
                    }
                }))
                .ToArray();

            await Task.WhenAll(mutators).WaitAsync(DefaultConditionTimeout);

            var expected = items.Where(x => finalActive[x.Id]).Select(x => x.Id).ToHashSet();
            WaitForCondition(() => results.Data.Keys.ToHashSet().SetEquals(expected));

            results.Data.Keys.Should().BeEquivalentTo(expected,
                $"iter {iter}: final filter contents should match the per-item finalActive map");
            results.Error.Should().BeNull($"iter {iter}: pipeline must not error");
        }
    }


    private static Deep1 NewDeepChain(int leaf) =>
        new Deep1 { Child = NewDeep2(leaf) };

    private static Deep2 NewDeep2(int leaf) =>
        new Deep2 { Child = NewDeep3(leaf) };

    private static Deep3 NewDeep3(int leaf) =>
        new Deep3 { Child = NewDeep4(leaf) };

    private static Deep4 NewDeep4(int leaf) =>
        new Deep4 { Child = new Deep5 { Leaf = leaf } };

    private sealed class Deep1 : INotifyPropertyChanged
    {
        private Deep2? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep2? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep2 : INotifyPropertyChanged
    {
        private Deep3? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep3? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep3 : INotifyPropertyChanged
    {
        private Deep4? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep4? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep4 : INotifyPropertyChanged
    {
        private Deep5? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep5? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep5 : INotifyPropertyChanged
    {
        private int _leaf;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Leaf
        {
            get => _leaf;
            set
            {
                _leaf = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }

    private sealed class KeyedActivable : INotifyPropertyChanged
    {
        private bool _activated;

        public KeyedActivable(int id)
        {
            Id = id;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; }

        public bool Activated
        {
            get => _activated;
            set
            {
                _activated = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Activated)));
            }
        }
    }

    private static void WaitForCondition(Func<bool> condition, TimeSpan? timeout = null) =>
        SpinWait.SpinUntil(condition, timeout ?? DefaultConditionTimeout);
}
