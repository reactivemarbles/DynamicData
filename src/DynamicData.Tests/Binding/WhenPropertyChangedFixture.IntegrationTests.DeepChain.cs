namespace DynamicData.Tests.Binding;
 
public static partial class WhenPropertyChangedFixture
{
    public partial class IntegrationTests
    {
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
    }
}
