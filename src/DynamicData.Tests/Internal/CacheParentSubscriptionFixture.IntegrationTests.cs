// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed partial class CacheParentSubscriptionFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        private readonly Randomizer _rand = new(55);

        /// <summary>
        /// Two subscriptions whose emissions feed each other, both through the other's child observable and through
        /// the other's source cache. Each writer thread is parked inside its own subscription's delivery until the
        /// other is too, and only then feeds the other, so the cycle is exercised on every run. If either the
        /// subscription or the cache held its lock while delivering, each writer would block on the lock the other
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

            // The child is fed before the cache, so it only reaches the subscription made for the seed.
            var observerA = new CrossFeedObserver(rendezvous, () => { childOfB.OnNext(childValueFromA); sourceB.AddOrUpdate(crossFromA); });
            using var subA = new TestSubscription(observerA, _ => childOfA);
            subA.ExposeCreateParent(sourceA.Connect());

            var observerB = new CrossFeedObserver(rendezvous, () => { childOfA.OnNext(childValueFromB); sourceA.AddOrUpdate(crossFromB); });
            using var subB = new TestSubscription(observerB, _ => childOfB);
            subB.ExposeCreateParent(sourceB.Connect());

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
