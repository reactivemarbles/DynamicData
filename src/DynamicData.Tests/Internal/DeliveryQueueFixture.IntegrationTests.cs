// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public partial class DeliveryQueueFixture
{
    /// <summary>
    /// Tests that park a delivery on one thread while others contend for the queue. The parked and contending threads
    /// are dedicated and the tests run outside the parallel collection, so a starved thread pool cannot hold them up.
    /// </summary>
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        [Fact(Timeout = 10_000)]
        public async Task OnlyOneDelivererAtATime()
        {
            var concurrentCount = 0;
            var maxConcurrent = 0;
            var deliveryCount = 0;
            var delivered = new ConcurrentBag<int>();
            var firstDeliveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var allowFirstDeliveryToContinue = new ManualResetEventSlim(false);
            using var startContenders = new ManualResetEventSlim(false);

            var observer = new BlockingObserver<int>(
                onNextAction: item =>
                {
                    var current = Interlocked.Increment(ref concurrentCount);
                    int snapshot;
                    do
                    {
                        snapshot = maxConcurrent;
                        if (current <= snapshot) break;
                    }
                    while (Interlocked.CompareExchange(ref maxConcurrent, current, snapshot) != snapshot);

                    delivered.Add(item);

                    if (Interlocked.Increment(ref deliveryCount) == 1)
                    {
                        firstDeliveryStarted.SetResult();
                        allowFirstDeliveryToContinue.Wait();
                    }

                    Thread.SpinWait(1000);
                    Interlocked.Decrement(ref concurrentCount);
                });

            var queue = new DeliveryQueue<int>(_gate, observer);

            var firstDelivery = DedicatedThread.Run(() => EnqueueAndDeliver(queue, -1));
            await firstDeliveryStarted.Task;

            var enqueueTasks = Enumerable.Range(0, 100)
                .Select(i => DedicatedThread.Run(() =>
                {
                    startContenders.Wait();
                    EnqueueAndDeliver(queue, i);
                }));

            var triggerTasks = Enumerable.Range(0, 4)
                .Select(_ => DedicatedThread.Run(() =>
                {
                    startContenders.Wait();
                    TriggerDelivery(queue);
                }));

            var tasks = enqueueTasks.Concat(triggerTasks).ToArray();
            startContenders.Set();
            allowFirstDeliveryToContinue.Set();

            await Task.WhenAll(tasks.Append(firstDelivery));

            maxConcurrent.Should().Be(1, "only one thread should be delivering at a time");
            delivered.Should().HaveCount(101);
        }

        [Fact(Timeout = 10_000)]
        public async Task DisposeFromDrainThreadDoesNotDeadlock()
        {
            var observer = new ListObserver<string>();
            DeliveryQueue<string>? q = null;

            var terminatingObserver = new DelegateObserver<string>(_ =>
            {
                // Called from drain thread: Dispose must detect
                // re-entrancy via _drainThreadId and skip the spin-wait
                q!.Dispose();
            });

            var queue = new DeliveryQueue<string>(_gate, terminatingObserver);
            q = queue;

            // This should NOT deadlock
            await DedicatedThread.Run(() => EnqueueAndDeliver(queue, "A"));
        }

        [Fact(Timeout = 10_000)]
        public async Task DisposeWaitsForInFlightDelivery()
        {
            var observer = new ListObserver<int>();
            var deliveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var allowDeliveryToFinish = new ManualResetEventSlim(false);

            var slowObserver = new DelegateObserver<int>(item =>
            {
                observer.OnNext(item);
                deliveryStarted.SetResult();
                allowDeliveryToFinish.Wait();
            });

            var queue = new DeliveryQueue<int>(_gate, slowObserver);

            // Start delivering: it will block in observer
            var deliverTask = DedicatedThread.Run(() => EnqueueAndDeliver(queue, 42));
            await deliveryStarted.Task;

            // Drain thread is blocked in observer callback. Dispose should spin.
            var terminateTask = DedicatedThread.Run(() => queue.Dispose());

            // Give terminate a moment to enter spin-wait
            await Task.Delay(100);
            terminateTask.IsCompleted.Should().BeFalse("should be spinning waiting for delivery");

            // Release the delivery
            allowDeliveryToFinish.Set();

            await Task.WhenAll(deliverTask, terminateTask);
            queue.IsTerminated.Should().BeTrue();
            observer.Items.Should().Equal(42);
        }
    }
}
