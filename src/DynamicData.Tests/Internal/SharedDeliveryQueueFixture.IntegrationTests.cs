// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public partial class SharedDeliveryQueueFixture
{
    /// <summary>
    /// Tests that park a delivery on one thread while another raises notifications. The drainer runs on a thread of
    /// its own and the tests run outside the parallel collection, so a starved thread pool cannot hold them up.
    /// </summary>
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        private readonly Randomizer _randomizer = new(0x1162_5EED);

#if NET9_0_OR_GREATER
        private readonly Lock _gate = new();
#else
        private readonly object _gate = new();
#endif

        [Fact(Timeout = 10_000)]
        public async Task ReentrantNotificationIsDeliveredBeforeNotificationsQueuedByOtherThreads()
        {
            // Arrange
            var (first, second, childValue) = (_randomizer.Int(), _randomizer.Int(), _randomizer.Word());
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var isFirst = true;
            DeliverySubQueue<string>? child = null;

            var parent = queue.CreateQueue(new TestObserver<int>(value =>
            {
                Record(delivered, $"parent:{value}:start");

                if (isFirst)
                {
                    isFirst = false;
                    parked.SetResult();
                    release.Wait();
                    child!.OnNext(childValue);
                }

                Record(delivered, $"parent:{value}:end");
            }));

            child = queue.CreateQueue(new TestObserver<string>(value => Record(delivered, $"child:{value}")));

            var drainer = DedicatedThread.Run(() => parent.OnNext(first));
            await parked.Task;
            parent.OnNext(second);

            // Act
            release.Set();
            await drainer;

            // Assert
            delivered.Should().Equal(
                new[] { $"parent:{first}:start", $"child:{childValue}", $"parent:{first}:end", $"parent:{second}:start", $"parent:{second}:end" },
                "a notification raised during a delivery is delivered inline, and one queued by another thread waits for that delivery to finish");
        }

        [Fact(Timeout = 10_000)]
        public async Task ReentrantNotificationForTheDeliveringSourceWaitsForItsTurn()
        {
            // Arrange
            var (first, second, third) = (_randomizer.Int(), _randomizer.Int(), _randomizer.Int());
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var isFirst = true;
            DeliverySubQueue<int>? source = null;

            source = queue.CreateQueue(new TestObserver<int>(value =>
            {
                Record(delivered, $"{value}:start");

                if (isFirst)
                {
                    isFirst = false;
                    parked.SetResult();
                    release.Wait();
                    source!.OnNext(third);
                }

                Record(delivered, $"{value}:end");
            }));

            var drainer = DedicatedThread.Run(() => source.OnNext(first));
            await parked.Task;
            source.OnNext(second);

            // Act
            release.Set();
            await drainer;

            // Assert
            delivered.Should().Equal(
                new[] { $"{first}:start", $"{first}:end", $"{second}:start", $"{second}:end", $"{third}:start", $"{third}:end" },
                "an observer is never re-entered, so its own reentrant notification is delivered in the order it was received");
        }

        [Fact(Timeout = 10_000)]
        public async Task InlineDeliveryKeepsTheSourcesEarlierNotificationsFirst()
        {
            // Arrange
            var (first, second) = (_randomizer.Int(), _randomizer.Int());
            var (queuedChildValue, reentrantChildValue) = (_randomizer.Word(), _randomizer.Word());
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var isFirst = true;
            DeliverySubQueue<string>? child = null;

            var parent = queue.CreateQueue(new TestObserver<int>(value =>
            {
                Record(delivered, $"parent:{value}:start");

                if (isFirst)
                {
                    isFirst = false;
                    parked.SetResult();
                    release.Wait();
                    child!.OnNext(reentrantChildValue);
                }

                Record(delivered, $"parent:{value}:end");
            }));

            child = queue.CreateQueue(new TestObserver<string>(value => Record(delivered, $"child:{value}")));

            var drainer = DedicatedThread.Run(() => parent.OnNext(first));
            await parked.Task;
            parent.OnNext(second);
            child.OnNext(queuedChildValue);

            // Act
            release.Set();
            await drainer;

            // Assert
            delivered.Should().Equal(
                new[] { $"parent:{first}:start", $"child:{queuedChildValue}", $"child:{reentrantChildValue}", $"parent:{first}:end", $"parent:{second}:start", $"parent:{second}:end" },
                "a source's notifications stay in the order it raised them, even when the later one is delivered inline");
        }

        [Fact(Timeout = 10_000)]
        public async Task InlineDeliveryDoesNotLetLaterNotificationsJumpTheQueue()
        {
            // Arrange
            var (first, second, third) = (_randomizer.Int(), _randomizer.Int(), _randomizer.Int());
            var (queuedChildValue, reentrantChildValue, laterChildValue) = (_randomizer.Word(), _randomizer.Word(), _randomizer.Word());
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var parkedAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            using var releaseAgain = new ManualResetEventSlim();
            var isFirst = true;
            DeliverySubQueue<string>? child = null;

            var parent = queue.CreateQueue(new TestObserver<int>(value =>
            {
                Record(delivered, $"parent:{value}:start");

                if (isFirst)
                {
                    isFirst = false;
                    parked.SetResult();
                    release.Wait();
                    child!.OnNext(reentrantChildValue);
                    parkedAgain.SetResult();
                    releaseAgain.Wait();
                }

                Record(delivered, $"parent:{value}:end");
            }));

            child = queue.CreateQueue(new TestObserver<string>(value => Record(delivered, $"child:{value}")));

            // The child's first two notifications are delivered inline, leaving their places in the receipt order
            // behind. The child's next notification has to wait for its own place, after the parent's third.
            var drainer = DedicatedThread.Run(() => parent.OnNext(first));
            await parked.Task;
            parent.OnNext(second);
            child.OnNext(queuedChildValue);
            parent.OnNext(third);
            release.Set();
            await parkedAgain.Task;
            child.OnNext(laterChildValue);

            // Act
            releaseAgain.Set();
            await drainer;

            // Assert
            delivered.Should().Equal(
                new[]
                {
                    $"parent:{first}:start", $"child:{queuedChildValue}", $"child:{reentrantChildValue}", $"parent:{first}:end",
                    $"parent:{second}:start", $"parent:{second}:end", $"parent:{third}:start", $"parent:{third}:end", $"child:{laterChildValue}",
                },
                "notifications delivered inline must not hand their places in the receipt order to later ones");
        }

        [Fact(Timeout = 10_000)]
        public async Task ReceiptOrderIsPreservedAcrossSubQueues()
        {
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var blockFirst = new ManualResetEventSlim(false);
            var firstIsDelivering = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
            {
                lock (delivered) { delivered.Add($"int:{i}"); }

                if (i == 1)
                {
                    firstIsDelivering.SetResult();
                    blockFirst.Wait();
                }
            }));

            var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
            {
                lock (delivered) { delivered.Add($"str:{s}"); }
            }));

            // Park a drain part-way through, so the notifications below get queued rather than
            // delivered inline.
            var drainer = DedicatedThread.Run(() =>
            {
                using var scope = sub1.AcquireLock();
                scope.EnqueueNext(1);
            });

            await firstIsDelivering.Task;

            using (var scope = sub1.AcquireLock())
            {
                scope.EnqueueNext(2);
            }

            using (var scope = sub2.AcquireLock())
            {
                scope.EnqueueNext("hello");
            }

            blockFirst.Set();
            await drainer;

            delivered.Should().Equal(new[] { "int:1", "int:2", "str:hello" }, "delivery should follow the order the notifications were received, not the order the sub-queues were created");
        }

        [Fact(Timeout = 10_000)]
        public async Task InterleavedSubQueuesDeliverInReceiptOrder()
        {
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var block = new ManualResetEventSlim(false);
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
            {
                lock (delivered) { delivered.Add($"int:{i}"); }

                if (i == 0)
                {
                    parked.SetResult();
                    block.Wait();
                }
            }));

            var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
            {
                lock (delivered) { delivered.Add($"str:{s}"); }
            }));

            var drainer = DedicatedThread.Run(() =>
            {
                using var scope = sub1.AcquireLock();
                scope.EnqueueNext(0);
            });

            await parked.Task;

            using (var scope = sub2.AcquireLock())
            {
                scope.EnqueueNext("a");
            }

            using (var scope = sub1.AcquireLock())
            {
                scope.EnqueueNext(2);
            }

            using (var scope = sub2.AcquireLock())
            {
                scope.EnqueueNext("b");
            }

            using (var scope = sub1.AcquireLock())
            {
                scope.EnqueueNext(4);
            }

            block.Set();
            await drainer;

            delivered.Should().Equal("int:0", "str:a", "int:2", "str:b", "int:4");
        }

        [Fact(Timeout = 10_000)]
        public async Task DisposedSubQueueDoesNotDeliverQueuedItems()
        {
            var queue = new SharedDeliveryQueue(_gate);
            var delivered = new List<string>();
            var block = new ManualResetEventSlim(false);
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
            {
                lock (delivered) { delivered.Add($"int:{i}"); }

                if (i == 0)
                {
                    parked.SetResult();
                    block.Wait();
                }
            }));

            var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
            {
                lock (delivered) { delivered.Add($"str:{s}"); }
            }));

            var drainer = DedicatedThread.Run(() =>
            {
                using var scope = sub1.AcquireLock();
                scope.EnqueueNext(0);
            });

            await parked.Task;

            using (var scope = sub2.AcquireLock())
            {
                scope.EnqueueNext("dropped");
            }

            sub2.Dispose();

            block.Set();
            await drainer;

            delivered.Should().Equal(new[] { "int:0" }, "a disposed sub-queue should not deliver what it had queued");
        }
    }
}
