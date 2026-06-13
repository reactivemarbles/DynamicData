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

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Regression tests for the TOCTOU race in <c>WhenPropertyChanged</c> / <c>WhenValueChanged</c>.
/// The bug: <c>ObservablePropertyFactory</c> uses <c>initial.Concat(events)</c>, which subscribes
/// to the event source only AFTER the initial value is delivered. Any <c>PropertyChanged</c>
/// notification fired during that gap is silently dropped.
/// </summary>
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
                // Hold the OnNext open. With the BUG, Concat is blocked here and the propertyChanged
                // event handler has NOT yet been attached; a mutation now will be silently lost.
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
    public void NotifyInitialFalse_DoesNotDedupSameValuedEvents()
    {
        // Regression for the dedup-window bug: when notifyOnInitialValue is false, the one-shot
        // equality guard must NOT be armed; two consecutive PropertyChanged events with the same
        // value are both legitimate and both must reach the observer.
        var model = new TestModel { Value = 10 };
        var shallowEmissions = new List<int>();
        using var shallowSub = model.WhenPropertyChanged(m => m.Value, notifyOnInitialValue: false)
            .Subscribe(pv => { lock (shallowEmissions) shallowEmissions.Add(pv.Value); });

        model.Value = 42;
        model.Value = 42;
        WaitForCondition(() => { lock (shallowEmissions) return shallowEmissions.Count >= 2; });

        lock (shallowEmissions)
        {
            shallowEmissions.Should().Equal(new[] { 42, 42 }, "both same-valued events must be delivered when no initial value was requested");
        }

        var parent = new ParentModel { Child = new ChildModel { Age = 1 } };
        var deepEmissions = new List<int>();
        using var deepSub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
            .Subscribe(pv => { lock (deepEmissions) deepEmissions.Add(pv.Value); });

        parent.Child!.Age = 7;
        parent.Child!.Age = 7;
        WaitForCondition(() => { lock (deepEmissions) return deepEmissions.Count >= 2; });

        lock (deepEmissions)
        {
            deepEmissions.Should().Equal(new[] { 7, 7 }, "deep chain must also not dedupe when no initial value was requested");
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
        // Deterministic forcing of the deep-chain TOCTOU: the worker thread blocks inside the
        // initial-emit OnNext while the test thread mutates the leaf. With SharedDeliveryQueue the
        // mutation enqueues onto the signal sub-queue and returns immediately; when the observer
        // unblocks, the drainer processes the signal and delivers the resulting value.
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
        // 50 iterations is plenty: with SharedDeliveryQueue the outcome is deterministic, so a
        // single iteration is sufficient to prove correctness. 50 just adds defence in depth against
        // any future regression.
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

    private static void WaitForCondition(Func<bool> condition, TimeSpan? timeout = null) =>
        SpinWait.SpinUntil(condition, timeout ?? DefaultConditionTimeout);
}
