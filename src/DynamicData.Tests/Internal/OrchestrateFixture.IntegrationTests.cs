// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed partial class OrchestrateFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        private readonly Randomizer _rand = new(55);

        /// <summary>
        /// Two orchestrations whose emissions feed each other, both through the other's inner observable and through
        /// the other's source cache. Each writer thread is parked inside its own orchestration's delivery until the
        /// other is too, and only then feeds the other, so the cycle is exercised on every run. If either the
        /// orchestration or the cache held its lock while delivering, each writer would block on the lock the other
        /// holds.
        /// </summary>
        [Fact(Timeout = 10_000)]
        public async Task CrossFeedingSubscriptions_FeedEachOtherDuringDelivery_DoNotDeadlock()
        {
            // Arrange
            var keys = _rand.Shuffle(Enumerable.Range(1, 1_000)).Take(4).ToArray();
            var (seedA, seedB) = (new TestItem(keys[0], _rand.String2(8)), new TestItem(keys[1], _rand.String2(8)));
            var (crossFromA, crossFromB) = (new TestItem(keys[2], _rand.String2(8)), new TestItem(keys[3], _rand.String2(8)));
            var (childValueFromA, childValueFromB) = (_rand.String2(8), _rand.String2(8));

            using var sourceA = new SourceCache<TestItem, int>(x => x.Key);
            using var sourceB = new SourceCache<TestItem, int>(x => x.Key);
            using var childOfA = new Subject<string>();
            using var childOfB = new Subject<string>();
            using var rendezvous = new Barrier(2);

            // The inner observable is fed before the cache, so it only reaches the subscription made for the seed.
            var observerA = new CrossFeedObserver(rendezvous, () => { childOfB.OnNext(childValueFromA); sourceB.AddOrUpdate(crossFromA); });
            var (observableA, _) = Wire(sourceA.Connect(), _ => childOfA);
            using var subA = observableA.Subscribe(observerA);

            var observerB = new CrossFeedObserver(rendezvous, () => { childOfA.OnNext(childValueFromB); sourceA.AddOrUpdate(crossFromB); });
            var (observableB, _) = Wire(sourceB.Connect(), _ => childOfB);
            using var subB = observableB.Subscribe(observerB);

            // Act: the writers get threads of their own, so they never wait on a starved thread pool.
            var writerA = Task.Factory.StartNew(() => sourceA.AddOrUpdate(seedA), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var writerB = Task.Factory.StartNew(() => sourceB.AddOrUpdate(seedB), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await Task.WhenAll(writerA, writerB);

            // Assert
            sourceA.Items.Should().BeEquivalentTo([seedA, crossFromB]);
            sourceB.Items.Should().BeEquivalentTo([seedB, crossFromA]);
            observerA.Received.Should().BeEquivalentTo([seedA, seedA with { Value = childValueFromB }, crossFromB]);
            observerB.Received.Should().BeEquivalentTo([seedB, seedB with { Value = childValueFromA }, crossFromA]);
        }

        /// <summary>
        /// The source and an inner observable are driven from two threads at once. Every hook call is logged as a
        /// start and end pair, and the pairs must never interleave. A producer only returns once the notification it
        /// raised has been delivered, either by itself or by the thread that was already delivering, so the log is
        /// complete when both producers have returned.
        /// </summary>
        [Fact(Timeout = 10_000)]
        public async Task Serialization_ParentAndChildDoNotInterleave()
        {
            // Arrange
            var iterations = _rand.Number(50, 150);
            var key = _rand.Number(1, 1_000);
            var callLog = new List<string>();
            using var source = new SourceCache<TestItem, int>(x => x.Key);
            using var childSubject = new Subject<string>();
            var (observable, _) = Wire(
                source.Connect(),
                childFactory: _ => childSubject,
                onParent: () => LogCall(callLog, "P"),
                onChild: () => LogCall(callLog, "C"));
            using var sub = observable.Subscribe(new TestObserver());
            source.AddOrUpdate(new TestItem(key, _rand.String2(8)));

            // Act
            using var barrier = new Barrier(2);
            var parentTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < iterations; i++)
                    source.Refresh(new TestItem(key, string.Empty));
            });
            var childTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < iterations; i++)
                    childSubject.OnNext(i.ToString(CultureInfo.InvariantCulture));
            });
            await Task.WhenAll(parentTask, childTask);

            // Assert
            callLog.Should().HaveCount(4 * (iterations + 1) - 2, "every refresh and every inner value, plus the initial add, gets exactly one start and end pair");
            for (var i = 0; i < callLog.Count; i += 2)
                callLog[i + 1].Should().Be(callLog[i].Replace("start", "end"), "source and inner hooks must not interleave");
        }

        /// <summary>
        /// Several threads emit on their own inner observables at once. Every value must reach
        /// <see cref="CacheOrchestrator{TSource, TKey, TInner, TResult}.OnItemSourceNext"/>, whichever thread ends
        /// up delivering it.
        /// </summary>
        [Fact(Timeout = 10_000)]
        public async Task ConcurrentInnerEmissions_AllItemsReachOnItemSourceNext()
        {
            // Arrange
            var producerCount = _rand.Number(4, 8);
            var emissionsPerProducer = _rand.Number(100, 300);
            using var source = new SourceCache<TestItem, int>(x => x.Key);
            var innerSubjects = Enumerable.Range(1, producerCount).ToDictionary(i => i, _ => new Subject<string>());
            var (observable, getOrchestrator) = Wire(source.Connect(), key => innerSubjects[key]);
            using var sub = observable.Subscribe(new TestObserver());
            source.AddOrUpdate(innerSubjects.Keys.Select(key => new TestItem(key, _rand.String2(8))));
            var orchestrator = getOrchestrator();
            var expected = innerSubjects.Keys.ToDictionary(key => key, key => Enumerable.Range(0, emissionsPerProducer).Select(i => $"{key}-{i}").ToList());

            // Act
            using var barrier = new Barrier(producerCount);
            var producers = innerSubjects.Select(pair => Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < emissionsPerProducer; i++)
                        pair.Value.OnNext($"{pair.Key}-{i}");
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default));
            await Task.WhenAll(producers);

            // Assert
            var received = orchestrator.ChildCalls.GroupBy(call => call.Key).ToDictionary(group => group.Key, group => group.Select(call => call.Value).ToList());
            received.Keys.Should().BeEquivalentTo(expected.Keys);
            foreach (var (key, values) in expected)
                received[key].Should().Equal(values, "every value an inner observable emits must reach OnItemSourceNext exactly once, in order");
            foreach (var subject in innerSubjects.Values)
                subject.Dispose();
        }

        // Start and end are logged under separate locks, so a hook running concurrently can land between them.
        private static void LogCall(List<string> callLog, string prefix)
        {
            lock (callLog)
                callLog.Add($"{prefix}-start");

            lock (callLog)
                callLog.Add($"{prefix}-end");
        }

        /// <summary>
        /// Records every item it receives. On its first emission it waits at <paramref name="rendezvous"/> for the
        /// other writer, then runs <paramref name="crossFeed"/>.
        /// </summary>
        private sealed class CrossFeedObserver(Barrier rendezvous, Action crossFeed) : IObserver<IChangeSet<TestItem, int>>
        {
            private bool _hasCrossFed;

            public List<TestItem> Received { get; } = [];

            public void OnNext(IChangeSet<TestItem, int> value)
            {
                Received.AddRange(value.Select(change => change.Current));

                if (_hasCrossFed)
                    return;

                _hasCrossFed = true;
                rendezvous.SignalAndWait();
                crossFeed();
            }

            public void OnError(Exception error) { }

            public void OnCompleted() { }
        }
    }
}
