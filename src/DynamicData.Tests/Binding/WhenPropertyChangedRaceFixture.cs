// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;

using DynamicData.Binding;

using FluentAssertions;

namespace DynamicData.Tests.Binding;

/// <summary>
/// Multi-threaded race tests for <see cref="NotifyPropertyChangedEx.WhenPropertyChanged{TObject, TProperty}"/>.
/// Each test forces concurrency between the operator's subscribe call (or chain re-walk) and one or more
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> notifiers firing on other threads.
/// </summary>
public sealed class WhenPropertyChangedRaceFixture
{
    private static readonly TimeSpan ConditionTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Shallow_ConcurrentMutationDuringInitialEmit_NotDropped()
    {
        var item = new Item()
        {
            Id = 1,
            Value = 10
        };

        var whenSubscribing = new ManualResetEventSlim();
        var whenValueChanged = new ManualResetEventSlim();

        var source = item.WhenPropertyChanged(
            propertyAccessor:       static item => item.Value,
            notifyOnInitialValue:   true);

        var observedValues = new List<int>();
        var observer = Observer.Create<PropertyValue<Item, int>>(propertyValue =>
        {
            observedValues.Add(propertyValue.Value);

            whenSubscribing.Set();
            whenValueChanged.Wait();
        });

        await Task.WhenAll(
            Task.Run(() =>
            {
                using var subscription = source.Subscribe(observer);
            }),
            Task.Run(() =>
            {
                whenSubscribing.Wait();

                item.Value = 20;

                whenValueChanged.Set();
            }));

        observedValues.Should().BeEquivalentTo(
            expectation:    new [] { 10, 20 },
            config:         options => options.WithStrictOrdering(),
            because:        "All change events occurring after publication of the initial value should be captured and forwarded.");
    }

    [Fact]
    public async Task DeepChain_ConcurrentLeafMutationDuringInitialEmit_NotDropped()
    {
        // Deep-chain version of the above. The observer blocks inside its OnNext for the initial
        // leaf value while a second thread mutates the leaf.
        var parent = new ParentModel { Child = new ChildModel { Age = 10 } };

        var whenSubscribing = new ManualResetEventSlim();
        var whenValueChanged = new ManualResetEventSlim();

        var emissions = new List<int>();
        var observer = Observer.Create<PropertyValue<ParentModel, int>>(pv =>
        {
            emissions.Add(pv.Value);
            whenSubscribing.Set();
            whenValueChanged.Wait();
        });

        var source = parent.WhenPropertyChanged(static p => p.Child!.Age, notifyOnInitialValue: true);

        await Task.WhenAll(
            Task.Run(() =>
            {
                using var subscription = source.Subscribe(observer);
            }),
            Task.Run(() =>
            {
                whenSubscribing.Wait();
                parent.Child!.Age = 20;
                whenValueChanged.Set();
            })).WaitAsync(ConditionTimeout);

        emissions.Should().Equal(new[] { 10, 20 });
    }

    [Fact]
    public async Task DeepChain_ConcurrentParentSwap_LeafEventOnWinnerNotDropped()
    {
        // Two threads concurrently swap parent.Child. After both swaps complete, a leaf mutation
        // on the current child must be captured. SharedDeliveryQueue serialises the level-0
        // signals on the drainer, so the final level-1 subscription always targets parent.Child's
        // current value.
        const int iterations = 50;
        var losses = 0;

        for (var iter = 0; iter < iterations; iter++)
        {
            var parent = new ParentModel { Child = new ChildModel { Age = 0 } };
            var emissions = new List<int>();

            using var sub = parent.WhenPropertyChanged(p => p.Child!.Age, notifyOnInitialValue: false)
                .Subscribe(pv => { lock (emissions) emissions.Add(pv.Value); });

            var newChild1 = new ChildModel { Age = 1 };
            var newChild2 = new ChildModel { Age = 2 };

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); parent.Child = newChild1; });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); parent.Child = newChild2; });
            await Task.WhenAll(taskA, taskB).WaitAsync(ConditionTimeout);

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

        losses.Should().Be(0, $"out of {iterations} iterations, {losses} dropped the leaf event on the post-swap winner");
    }

    [Fact]
    public async Task DeepChain_FiveLevels_AllLevelsMutatedConcurrently_FinalEmissionMatchesActual()
    {
        // Torture: five worker threads each mutating at a different level of a 5-level chain.
        // Mutations that land on detached subtrees are ignored (their notifier subscriptions were
        // disposed by ResubscribeFrom). Mutations on the live chain reach the drainer.
        //
        // Three invariants per iteration:
        //   (a) Rx contract: ValidateSynchronization catches any concurrent OnNext on the user
        //       observer (a SharedDeliveryQueue serialisation failure).
        //   (b) Value legality: every emission must be a value that some thread legitimately
        //       wrote.
        //   (c) Final consistency: after Task.WhenAll the drainer continues until the queue is
        //       empty. The last processed signal triggers a ReadCurrent against the now-frozen
        //       chain state, so emissions.Last() == ReadCurrent().
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

            await Task.WhenAll(tasks).WaitAsync(ConditionTimeout);

            var actualFinal = root.Child!.Child!.Child!.Child!.Leaf;

            WaitForCondition(() => { lock (emissions) return emissions.Count > 0 && emissions[^1] == actualFinal; });

            var legal = new HashSet<int> { 0 };
            for (var i = 0; i < mutationsPerThread; i++)
            {
                legal.Add(i);
                legal.Add(iterSeed + 10_000 + i);
                legal.Add(iterSeed + 20_000 + i);
                legal.Add(iterSeed + 30_000 + i);
                legal.Add(iterSeed + 40_000 + i);
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

        mismatches.Should().Be(0, $"out of {iterations} iterations, {mismatches} ended with the last emission not matching the actual final chain leaf");
    }

    [Fact(Skip = "AutoRefresh has a separate concurrency bug; tracked separately")]
    public async Task AutoRefreshThenFilter_ConcurrentAddsAndPropertyActivation_AllItemsObserved()
    {
        // One adder thread sequentially adds items to the cache while a single flipper thread
        // concurrently sets each item's Activated to true. Final filter contents must include
        // every item (every item ends Activated=true).
        //
        // KeyedActivable's setter only raises PropertyChanged on actual value change, so a
        // dropped false->true transition is unrecoverable.
        //
        // The race lives in AutoRefresh's internal Publish multicast: Sub 1 (Filter path)
        // receives the Add and reads the property before Sub 2 (MergeMany) subscribes the
        // per-item refresh handler. A concurrent flip landing in that gap is dropped. This
        // is not a WhenPropertyChanged issue: AutoRefresh calls WhenPropertyChanged with
        // notifyInitial=false, so the per-item subscribe attaches the handler immediately
        // and has no internal race window.
        const int iterations = 100;
        const int itemCount = 200;

        for (var iter = 0; iter < iterations; iter++)
        {
            using var cache = new SourceCache<KeyedActivable, int>(x => x.Id);
            var items = Enumerable.Range(0, itemCount).Select(i => new KeyedActivable(i)).ToList();

            using var results = cache.Connect()
                .AutoRefresh(x => x.Activated)
                .Filter(x => x.Activated)
                .AsAggregator();

            using var barrier = new Barrier(2);

            var adder = Task.Run(() =>
            {
                barrier.SignalAndWait();
                foreach (var item in items) cache.AddOrUpdate(item);
            });

            var flipper = Task.Run(() =>
            {
                barrier.SignalAndWait();
                foreach (var item in items) item.Activated = true;
            });

            await Task.WhenAll(adder, flipper).WaitAsync(ConditionTimeout);

            var expected = items.Select(x => x.Id).ToHashSet();
            WaitForCondition(() => results.Data.Keys.ToHashSet().SetEquals(expected));

            var actual = results.Data.Keys.ToHashSet();
            actual.Should().BeEquivalentTo(expected, $"iter {iter}: every item ends Activated=true and must appear in the filter (missing: {string.Join(",", expected.Except(actual))})");
            results.Error.Should().BeNull($"iter {iter}: pipeline must not error");
        }
    }

    [Fact(Skip = "AutoRefresh has a separate concurrency bug; tracked separately")]
    public async Task AutoRefreshThenFilter_DualSubscribers_AllItemsObserved()
    {
        // Two independent cache subscribers running on the ThreadPool:
        //   Sub 1 (mutator): on every Add change, flips item.Activated to true
        //   Sub 2 (filter chain): AutoRefresh + Filter (filter = Activated)
        // Items start with Activated=false (filtered out). The mutator flips every item, so
        // the final filter contents must include every item.
        //
        // Same root cause as the single-flipper variant above: AutoRefresh's internal Publish
        // multicasts the Add to the Filter path before MergeMany subscribes the per-item
        // refresh handler. The mutator's flip can land in that gap and be dropped.
        const int iterations = 100;
        const int itemCount = 200;

        for (var iter = 0; iter < iterations; iter++)
        {
            using var cache = new SourceCache<KeyedActivable, int>(x => x.Id);
            var items = Enumerable.Range(0, itemCount).Select(i => new KeyedActivable(i)).ToList();

            using var mutator = cache.Connect()
                .ObserveOn(TaskPoolSequencer.Default)
                .Subscribe(changes =>
                {
                    foreach (var change in changes)
                    {
                        if (change.Reason == ChangeReason.Add)
                        {
                            change.Current.Activated = true;
                        }
                    }
                });

            using var results = cache.Connect()
                .ObserveOn(TaskPoolSequencer.Default)
                .AutoRefresh(x => x.Activated)
                .Filter(x => x.Activated)
                .AsAggregator();

            foreach (var item in items) cache.AddOrUpdate(item);

            var expected = items.Select(x => x.Id).ToHashSet();
            WaitForCondition(() => results.Data.Keys.ToHashSet().SetEquals(expected));

            var actual = results.Data.Keys.ToHashSet();
            actual.Should().BeEquivalentTo(expected, $"iter {iter}: every item was flipped to Activated=true by the mutator and must appear in the filter (missing: {string.Join(",", expected.Except(actual))})");
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

    private static void WaitForCondition(Func<bool> condition, TimeSpan? timeout = null) =>
        SpinWait.SpinUntil(condition, timeout ?? ConditionTimeout);

    private sealed class Item : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; init; }

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
                if (_activated == value) return;
                _activated = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Activated)));
            }
        }
    }
}
