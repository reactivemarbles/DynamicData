// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed partial class DeliveryQueueMergeFixture
{
    /// <summary>
    /// Tests that hold producer threads at a <see cref="Barrier"/> so they race on the inputs. The producers are
    /// dedicated threads and the tests run outside the parallel collection, so a starved thread pool cannot hold them up.
    /// </summary>
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Fact(Timeout = 10_000)]
        public async Task ConcurrentOnNext_FromManyProducers_IsSerializedToObserver()
        {
            // The queue's contract is that the downstream observer never sees concurrent OnNext calls,
            // regardless of how many producers are racing on the inputs. Subscribe to two sources via
            // two concurrent tasks, push interleaved items, and verify that no two OnNext calls overlap
            // and every item is delivered exactly once.
            const int itemsPerProducer = 1_000;

            using var a = new Subject<int>();
            using var b = new Subject<int>();

            var inFlight = 0;
            var maxInFlight = 0;
            var received = new ConcurrentQueue<int>();

            using var sub = a.DeliveryQueueMerge(b).Subscribe(v =>
            {
                var now = Interlocked.Increment(ref inFlight);
                var prev = Volatile.Read(ref maxInFlight);
                while (now > prev && Interlocked.CompareExchange(ref maxInFlight, now, prev) != prev)
                {
                    prev = Volatile.Read(ref maxInFlight);
                }
                received.Enqueue(v);
                Interlocked.Decrement(ref inFlight);
            });

            using var barrier = new Barrier(2);
            var taskA = DedicatedThread.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < itemsPerProducer; i++) a.OnNext(i); });
            var taskB = DedicatedThread.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < itemsPerProducer; i++) b.OnNext(itemsPerProducer + i); });

            await Task.WhenAll(taskA, taskB);

            received.Count.Should().Be(itemsPerProducer * 2);
            maxInFlight.Should().Be(1, "concurrent OnNext to the observer must be serialized by the queue");

            var expected = Enumerable.Range(0, itemsPerProducer * 2).ToHashSet();
            received.Should().BeEquivalentTo(expected);
        }
    }
}
