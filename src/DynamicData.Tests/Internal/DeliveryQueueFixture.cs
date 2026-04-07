using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Internal;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

public class DeliveryQueueFixture
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    private static void EnqueueAndDeliver<T>(DeliveryQueue<T> queue, T item)
    {
        using var notifications = queue.AcquireLock();
        notifications.Enqueue(item);
    }

    private static void TriggerDelivery<T>(DeliveryQueue<T> queue)
    {
        using var notifications = queue.AcquireLock();
    }

    // Category 1: Basic Behavior

    [Fact]
    public void EnqueueAndDeliverDeliversItem()
    {
        var delivered = new List<string>();
        var queue = new DeliveryQueue<string>(_gate, item => { delivered.Add(item); return true; });

        EnqueueAndDeliver(queue, "A");

        delivered.Should().Equal("A");
    }

    [Fact]
    public void DeliverDeliversItemsInFifoOrder()
    {
        var delivered = new List<string>();
        var queue = new DeliveryQueue<string>(_gate, item => { delivered.Add(item); return true; });

        using (var notifications = queue.AcquireLock())
        {
            notifications.Enqueue("A");
            notifications.Enqueue("B");
            notifications.Enqueue("C");
        }

        delivered.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void DeliverWithEmptyQueueIsNoOp()
    {
        var delivered = new List<string>();
        var queue = new DeliveryQueue<string>(_gate, item => { delivered.Add(item); return true; });

        TriggerDelivery(queue);

        delivered.Should().BeEmpty();
    }

    // Category 2: Delivery Token Serialization

    [Fact]
    public async Task OnlyOneDelivererAtATime()
    {
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var queue = new DeliveryQueue<int>(_gate, _ =>
        {
            var current = Interlocked.Increment(ref concurrentCount);
            if (current > maxConcurrent)
            {
                Interlocked.Exchange(ref maxConcurrent, current);
            }

            Thread.SpinWait(1000);
            Interlocked.Decrement(ref concurrentCount);
            return true;
        });

        using (var notifications = queue.AcquireLock())
        {
            for (var i = 0; i < 100; i++)
            {
                notifications.Enqueue(i);
            }
        }

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() => TriggerDelivery(queue))).ToArray();
        await Task.WhenAll(tasks);

        maxConcurrent.Should().Be(1, "only one thread should be delivering at a time");
    }

    [Fact]
    public void SecondWriterItemPickedUpByFirstDeliverer()
    {
        var delivered = new List<string>();
        var deliveryCount = 0;
        DeliveryQueue<string>? q = null;

        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            delivered.Add(item);
            if (Interlocked.Increment(ref deliveryCount) == 1)
            {
                using var notifications = q!.AcquireLock();
                notifications.Enqueue("B");
            }

            return true;
        });
        q = queue;

        EnqueueAndDeliver(queue, "A");

        delivered.Should().Equal("A", "B");
    }

    [Fact]
    public void ReentrantEnqueueDoesNotRecurse()
    {
        var callDepth = 0;
        var maxDepth = 0;
        var delivered = new List<string>();
        DeliveryQueue<string>? q = null;

        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            callDepth++;
            if (callDepth > maxDepth)
            {
                maxDepth = callDepth;
            }

            delivered.Add(item);

            if (item == "A")
            {
                using var notifications = q!.AcquireLock();
                notifications.Enqueue("B");
            }

            callDepth--;
            return true;
        });
        q = queue;

        EnqueueAndDeliver(queue, "A");

        delivered.Should().Equal("A", "B");
        maxDepth.Should().Be(1, "delivery callback should not recurse");
    }

    // Category 3: Exception Safety

    [Fact]
    public void ExceptionInDeliveryResetsDeliveryToken()
    {
        var callCount = 0;
        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return true;
        });

        var act = () => EnqueueAndDeliver(queue, "A");
        act.Should().Throw<InvalidOperationException>();

        EnqueueAndDeliver(queue, "B");

        callCount.Should().Be(2, "delivery should work after exception recovery");
    }

    [Fact]
    public void RemainingItemsDeliveredAfterExceptionRecovery()
    {
        var delivered = new List<string>();
        var shouldThrow = true;
        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            if (shouldThrow && item == "A")
            {
                throw new InvalidOperationException("boom");
            }

            delivered.Add(item);
            return true;
        });

        var act = () =>
        {
            using var notifications = queue.AcquireLock();
            notifications.Enqueue("A");
            notifications.Enqueue("B");
        };

        act.Should().Throw<InvalidOperationException>();

        shouldThrow = false;
        TriggerDelivery(queue);

        delivered.Should().Equal("B");
    }

    // Category 4: Termination

    [Fact]
    public void TerminalCallbackStopsDelivery()
    {
        var delivered = new List<string>();
        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            delivered.Add(item);
            return item != "STOP";
        });

        using (var notifications = queue.AcquireLock())
        {
            notifications.Enqueue("A");
            notifications.Enqueue("STOP");
            notifications.Enqueue("B");
        }

        delivered.Should().Equal("A", "STOP");
        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void EnqueueAfterTerminationIsIgnored()
    {
        var delivered = new List<string>();
        var queue = new DeliveryQueue<string>(_gate, item =>
        {
            delivered.Add(item);
            return item != "STOP";
        });

        EnqueueAndDeliver(queue, "STOP");

        EnqueueAndDeliver(queue, "AFTER");

        delivered.Should().Equal("STOP");
    }

    [Fact]
    public void IsTerminatedIsFalseInitially()
    {
        var queue = new DeliveryQueue<string>(_gate, _ => true);
        queue.IsTerminated.Should().BeFalse();
    }

    // Category 5: PendingCount

    [Fact]
    public void PendingCountTracksAutomatically()
    {
        var queue = new DeliveryQueue<string>(_gate, _ => true);

        using (var notifications = queue.AcquireLock())
        {
            notifications.PendingCount.Should().Be(0);

            notifications.Enqueue("A", countAsPending: true);
            notifications.Enqueue("B", countAsPending: true);
            notifications.Enqueue("C");

            notifications.PendingCount.Should().Be(2);
        }

        using (var notifications = queue.AcquireLock())
        {
            notifications.PendingCount.Should().Be(0, "pending count should auto-decrement on delivery");
        }
    }

    [Fact]
    public void PendingCountPreservedOnException()
    {
        var callCount = 0;
        var queue = new DeliveryQueue<string>(_gate, _ =>
        {
            if (++callCount == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return true;
        });

        var act = () =>
        {
            using var notifications = queue.AcquireLock();
            notifications.Enqueue("A", countAsPending: true);
            notifications.Enqueue("B", countAsPending: true);
        };

        act.Should().Throw<InvalidOperationException>();

        using (var rl = queue.AcquireReadLock())
        {
            rl.PendingCount.Should().Be(1, "only the dequeued item should be decremented");
        }
    }

    [Fact]
    public void PendingCountClearedOnTermination()
    {
        var queue = new DeliveryQueue<string>(_gate, item => item != "STOP");

        using (var notifications = queue.AcquireLock())
        {
            notifications.Enqueue("A", countAsPending: true);
            notifications.Enqueue("B", countAsPending: true);
            notifications.Enqueue("STOP");
        }

        using (var rl = queue.AcquireReadLock())
        {
            rl.PendingCount.Should().Be(0);
        }
    }

    // Category 6: Stress / Thread Safety

    [Fact]
    public async Task ConcurrentEnqueueAllItemsDelivered()
    {
        const int threadCount = 8;
        const int itemsPerThread = 500;
        var delivered = new ConcurrentBag<int>();
        var queue = new DeliveryQueue<int>(_gate, item => { delivered.Add(item); return true; });

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
            {
                EnqueueAndDeliver(queue, (t * itemsPerThread) + i);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        delivered.Count.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task ConcurrentEnqueueNoDuplicates()
    {
        const int threadCount = 8;
        const int itemsPerThread = 500;
        var delivered = new ConcurrentBag<int>();
        var queue = new DeliveryQueue<int>(_gate, item => { delivered.Add(item); return true; });

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
            {
                EnqueueAndDeliver(queue, (t * itemsPerThread) + i);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        delivered.Distinct().Count().Should().Be(threadCount * itemsPerThread, "each item should be delivered exactly once");
    }

    [Fact]
    public async Task ConcurrentEnqueuePreservesPerThreadOrdering()
    {
        const int threadCount = 4;
        const int itemsPerThread = 200;
        var delivered = new ConcurrentQueue<(int Thread, int Seq)>();
        var queue = new DeliveryQueue<(int Thread, int Seq)>(_gate, item => { delivered.Enqueue(item); return true; });

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
            {
                EnqueueAndDeliver(queue, (t, i));
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        var itemsByThread = delivered.ToArray().GroupBy(x => x.Thread).ToDictionary(g => g.Key, g => g.Select(x => x.Seq).ToList());

        foreach (var (thread, sequences) in itemsByThread)
        {
            sequences.Should().BeInAscendingOrder($"items from thread {thread} should preserve enqueue order");
        }
    }
}