// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace DynamicData.Tests.Internal;

[NotInParallel]
public class SharedDeliveryQueueFixture
{
    private readonly Lock _gate = new();

    [Test]
    public async Task SingleSourceDeliversItems()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new List<int>();
        var observer = new TestObserver<int>(delivered.Add);
        var sub = queue.CreateQueue(observer);

        using (var scope = sub.AcquireLock())
        {
            scope.EnqueueNext(1);
            scope.EnqueueNext(2);
            scope.EnqueueNext(3);
        }

        await Assert.That(delivered).IsEquivalentTo(new[] { 1, 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task MultipleSourcesSerializeDelivery()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new List<string>();
        var obs1 = new TestObserver<int>(i => delivered.Add($"int:{i}"));
        var obs2 = new TestObserver<string>(s => delivered.Add($"str:{s}"));
        var sub1 = queue.CreateQueue(obs1);
        var sub2 = queue.CreateQueue(obs2);

        using (var scope1 = sub1.AcquireLock())
        {
            scope1.EnqueueNext(1);
        }

        using (var scope2 = sub2.AcquireLock())
        {
            scope2.EnqueueNext("hello");
        }

        await Assert.That(delivered).IsEquivalentTo(new[] { "int:1", "str:hello" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ErrorTerminatesAllSubQueues()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered1 = new List<int>();
        var delivered2 = new List<string>();
        var obs1 = new TestObserver<int>(delivered1.Add);
        var obs2 = new TestObserver<string>(delivered2.Add);
        var sub1 = queue.CreateQueue(obs1);
        var sub2 = queue.CreateQueue(obs2);

        using (var scope1 = sub1.AcquireLock())
        {
            scope1.EnqueueNext(1);
            scope1.EnqueueError(new InvalidOperationException("boom"));
        }

        await Assert.That(queue.IsTerminated).IsTrue();

        // Further enqueues should be ignored
        using (var scope2 = sub2.AcquireLock())
        {
            scope2.EnqueueNext("ignored");
        }

        await Assert.That(delivered1).IsEquivalentTo(new[] { 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(obs1.Error).IsNotNull();
        await Assert.That(delivered2).IsEmpty();
    }

    [Test]
    public async Task CompletionDoesNotTerminateParent()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered1 = new List<int>();
        var delivered2 = new List<string>();
        var obs1 = new TestObserver<int>(delivered1.Add);
        var obs2 = new TestObserver<string>(delivered2.Add);
        var sub1 = queue.CreateQueue(obs1);
        var sub2 = queue.CreateQueue(obs2);

        using (var scope1 = sub1.AcquireLock())
        {
            scope1.EnqueueNext(1);
            scope1.EnqueueCompleted();
        }

        await Assert.That(queue.IsTerminated).IsFalse();
        await Assert.That(obs1.IsCompleted).IsTrue();

        // Other sub-queue should still work
        using (var scope2 = sub2.AcquireLock())
        {
            scope2.EnqueueNext("still alive");
        }

        await Assert.That(delivered2).IsEquivalentTo(new[] { "still alive" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DisposeTerminatesAndWaits()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var observer = new TestObserver<int>(_ => { });
        var sub = queue.CreateQueue(observer);

        using (var scope = sub.AcquireLock())
        {
            scope.EnqueueNext(1);
        }

        queue.Dispose();

        await Assert.That(queue.IsTerminated).IsTrue();
    }

    [Test]
    public async Task ConcurrentMultiSourceDelivery()
    {
        const int threadCount = 4;
        const int itemsPerThread = 200;
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new ConcurrentBag<string>();

        var subQueues = Enumerable.Range(0, threadCount).Select(t =>
        {
            var obs = new TestObserver<int>(i => delivered.Add($"{t}:{i}"));
            return queue.CreateQueue(obs);
        }).ToArray();

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
            {
                using var scope = subQueues[t].AcquireLock();
                scope.EnqueueNext(i);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(delivered.Count).IsEqualTo(threadCount * itemsPerThread);

        // Each thread's items should all be present
        for (var t = 0; t < threadCount; t++)
        {
            var threadItems = delivered.Where(s => s.StartsWith($"{t}:")).Count();
            await Assert.That(threadItems).IsEqualTo(itemsPerThread);
        }
    }

    [Test]
    public async Task ReceiptOrderIsPreservedAcrossSubQueues()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new List<string>();
        var blockFirst = new ManualResetEventSlim(false);
        var firstIsDelivering = new ManualResetEventSlim(false);

        var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
        {
            lock (delivered) { delivered.Add($"int:{i}"); }

            if (i == 1)
            {
                firstIsDelivering.Set();
                blockFirst.Wait();
            }
        }));

        var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
        {
            lock (delivered) { delivered.Add($"str:{s}"); }
        }));

        // Park a drain part-way through, so the notifications below get queued rather than
        // delivered inline.
        var drainer = Task.Factory.StartNew(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(1);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Assert.That(firstIsDelivering.Wait(TimeSpan.FromSeconds(5))).IsTrue();

        using (var scope = sub1.AcquireLock())
        {
            scope.EnqueueNext(2);
        }

        using (var scope = sub2.AcquireLock())
        {
            scope.EnqueueNext("hello");
        }

        blockFirst.Set();
        await Assert.That(drainer.Wait(TimeSpan.FromSeconds(5))).IsTrue();

        await Assert.That(delivered).IsEquivalentTo(new[] { "int:1", "int:2", "str:hello" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task InterleavedSubQueuesDeliverInReceiptOrder()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new List<string>();
        var block = new ManualResetEventSlim(false);
        var parked = new ManualResetEventSlim(false);

        var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
        {
            lock (delivered) { delivered.Add($"int:{i}"); }

            if (i == 0)
            {
                parked.Set();
                block.Wait();
            }
        }));

        var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
        {
            lock (delivered) { delivered.Add($"str:{s}"); }
        }));

        var drainer = Task.Factory.StartNew(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(0);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Assert.That(parked.Wait(TimeSpan.FromSeconds(5))).IsTrue();

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
        await Assert.That(drainer.Wait(TimeSpan.FromSeconds(5))).IsTrue();

        await Assert.That(delivered).IsEquivalentTo(new[] { "int:0", "str:a", "int:2", "str:b", "int:4" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DisposedSubQueueDoesNotDeliverQueuedItems()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var delivered = new List<string>();
        var block = new ManualResetEventSlim(false);
        var parked = new ManualResetEventSlim(false);

        var sub1 = queue.CreateQueue(new TestObserver<int>(i =>
        {
            lock (delivered) { delivered.Add($"int:{i}"); }

            if (i == 0)
            {
                parked.Set();
                block.Wait();
            }
        }));

        var sub2 = queue.CreateQueue(new TestObserver<string>(s =>
        {
            lock (delivered) { delivered.Add($"str:{s}"); }
        }));

        var drainer = Task.Factory.StartNew(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(0);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Assert.That(parked.Wait(TimeSpan.FromSeconds(5))).IsTrue();

        using (var scope = sub2.AcquireLock())
        {
            scope.EnqueueNext("dropped");
        }

        sub2.Dispose();

        block.Set();
        await Assert.That(drainer.Wait(TimeSpan.FromSeconds(5))).IsTrue();

        await Assert.That(delivered).IsEquivalentTo(new[] { "int:0" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private sealed class TestObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public Exception? Error { get; private set; }
        public bool IsCompleted { get; private set; }

        public void OnNext(T value) => onNext(value);
        public void OnError(Exception error) => Error = error;
        public void OnCompleted() => IsCompleted = true;
    }
}
