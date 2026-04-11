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

    /// <summary>Helper observer that captures OnNext items into a list.</summary>
    private sealed class ListObserver<T> : IObserver<T>
    {
        private readonly List<T> _items = new();
        public IReadOnlyList<T> Items => _items;
        public Exception? Error { get; private set; }
        public bool IsCompleted { get; private set; }

        public void OnNext(T value) => _items.Add(value);
        public void OnError(Exception error) => Error = error;
        public void OnCompleted() => IsCompleted = true;
    }

    /// <summary>Thread-safe observer for concurrent tests.</summary>
    private sealed class ConcurrentObserver<T> : IObserver<T>
    {
        private readonly ConcurrentBag<T> _items = new();
        public ConcurrentBag<T> Items => _items;

        public void OnNext(T value) => _items.Add(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    /// <summary>Thread-safe ordered observer for concurrent tests.</summary>
    private sealed class ConcurrentQueueObserver<T> : IObserver<T>
    {
        private readonly ConcurrentQueue<T> _items = new();
        public ConcurrentQueue<T> Items => _items;

        public void OnNext(T value) => _items.Enqueue(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private static void EnqueueAndDeliver<T>(DeliveryQueue<T> queue, T item)
    {
        using var scope = queue.AcquireLock();
        scope.Enqueue(item);
    }

    private static void TriggerDelivery<T>(DeliveryQueue<T> queue)
    {
        using var scope = queue.AcquireLock();
    }

    [Fact]
    public void EnqueueAndDeliverDeliversItem()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        EnqueueAndDeliver(queue, "A");

        observer.Items.Should().Equal("A");
    }

    [Fact]
    public void DeliverDeliversItemsInFifoOrder()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.Enqueue("A");
            scope.Enqueue("B");
            scope.Enqueue("C");
        }

        observer.Items.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void DeliverWithEmptyQueueIsNoOp()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        TriggerDelivery(queue);

        observer.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyOneDelivererAtATime()
    {
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var deliveryCount = 0;
        var delivered = new ConcurrentBag<int>();
        using var firstDeliveryStarted = new ManualResetEventSlim(false);
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
                    firstDeliveryStarted.Set();
                    allowFirstDeliveryToContinue.Wait();
                }

                Thread.SpinWait(1000);
                Interlocked.Decrement(ref concurrentCount);
            });

        var queue = new DeliveryQueue<int>(_gate, observer);

        var firstDelivery = Task.Run(() => EnqueueAndDeliver(queue, -1));
        firstDeliveryStarted.Wait();

        var enqueueTasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                startContenders.Wait();
                EnqueueAndDeliver(queue, i);
            }));

        var triggerTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
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

    [Fact]
    public void SecondWriterItemPickedUpByFirstDeliverer()
    {
        var observer = new ListObserver<string>();
        DeliveryQueue<string>? q = null;

        var enqueuingObserver = new DelegateObserver<string>(item =>
        {
            observer.OnNext(item);
            if (observer.Items.Count == 1)
            {
                using var scope = q!.AcquireLock();
                scope.Enqueue("B");
            }
        });

        var queue = new DeliveryQueue<string>(_gate, enqueuingObserver);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        observer.Items.Should().Equal("A", "B");
    }

    [Fact]
    public void ReentrantEnqueueDoesNotRecurse()
    {
        var callDepth = 0;
        var maxDepth = 0;
        var delivered = new List<string>();
        DeliveryQueue<string>? q = null;

        var observer = new DelegateObserver<string>(item =>
        {
            callDepth++;
            if (callDepth > maxDepth) maxDepth = callDepth;

            delivered.Add(item);

            if (item == "A")
            {
                using var scope = q!.AcquireLock();
                scope.Enqueue("B");
            }

            callDepth--;
        });

        var queue = new DeliveryQueue<string>(_gate, observer);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        delivered.Should().Equal("A", "B");
        maxDepth.Should().Be(1, "delivery callback should not recurse");
    }

    [Fact]
    public void ExceptionInDeliveryResetsDeliveryToken()
    {
        var callCount = 0;
        var observer = new DelegateObserver<string>(_ =>
        {
            if (++callCount == 1)
                throw new InvalidOperationException("boom");
        });

        var queue = new DeliveryQueue<string>(_gate, observer);

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
        var observer = new DelegateObserver<string>(item =>
        {
            if (shouldThrow && item == "A")
                throw new InvalidOperationException("boom");
            delivered.Add(item);
        });

        var queue = new DeliveryQueue<string>(_gate, observer);

        var act = () =>
        {
            using var scope = queue.AcquireLock();
            scope.Enqueue("A");
            scope.Enqueue("B");
        };

        act.Should().Throw<InvalidOperationException>();

        shouldThrow = false;
        TriggerDelivery(queue);

        delivered.Should().Equal("B");
    }

    [Fact]
    public void TerminalCompletedStopsDelivery()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.Enqueue("A");
            scope.EnqueueCompleted();
            scope.Enqueue("B"); // should be ignored after terminal
        }

        observer.Items.Should().Equal("A");
        observer.IsCompleted.Should().BeTrue();
        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void TerminalErrorStopsDelivery()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        var error = new InvalidOperationException("test");

        using (var scope = queue.AcquireLock())
        {
            scope.Enqueue("A");
            scope.EnqueueError(error);
            scope.Enqueue("B"); // should be ignored after terminal
        }

        observer.Items.Should().Equal("A");
        observer.Error.Should().BeSameAs(error);
        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void EnqueueAfterTerminationIsIgnored()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueCompleted();
        }

        EnqueueAndDeliver(queue, "AFTER");

        observer.Items.Should().BeEmpty();
    }

    [Fact]
    public void IsTerminatedIsFalseInitially()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        queue.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentEnqueueAllItemsDelivered()
    {
        const int threadCount = 8;
        const int itemsPerThread = 500;
        var observer = new ConcurrentObserver<int>();
        var queue = new DeliveryQueue<int>(_gate, observer);

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
                EnqueueAndDeliver(queue, (t * itemsPerThread) + i);
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        observer.Items.Count.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task ConcurrentEnqueueNoDuplicates()
    {
        const int threadCount = 8;
        const int itemsPerThread = 500;
        var observer = new ConcurrentObserver<int>();
        var queue = new DeliveryQueue<int>(_gate, observer);

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
                EnqueueAndDeliver(queue, (t * itemsPerThread) + i);
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        observer.Items.Distinct().Count().Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task ConcurrentEnqueuePreservesPerThreadOrdering()
    {
        const int threadCount = 4;
        const int itemsPerThread = 200;
        var observer = new ConcurrentQueueObserver<(int Thread, int Seq)>();
        var queue = new DeliveryQueue<(int Thread, int Seq)>(_gate, observer);

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerThread; i++)
                EnqueueAndDeliver(queue, (t, i));
        })).ToArray();

        await Task.WhenAll(tasks);
        TriggerDelivery(queue);

        var itemsByThread = observer.Items.ToArray().GroupBy(x => x.Thread)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Seq).ToList());

        foreach (var (thread, sequences) in itemsByThread)
            sequences.Should().BeInAscendingOrder($"items from thread {thread} should preserve enqueue order");
    }

    /// <summary>Observer that delegates OnNext to an action.</summary>
    private sealed class DelegateObserver<T>(Action<T> onNextAction) : IObserver<T>
    {
        public void OnNext(T value) => onNextAction(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    /// <summary>Observer with blocking capability for concurrency tests.</summary>
    private sealed class BlockingObserver<T>(Action<T> onNextAction) : IObserver<T>
    {
        public void OnNext(T value) => onNextAction(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    [Fact]
    public void EnsureDeliveryCompleteTerminatesQueue()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        EnqueueAndDeliver(queue, "A");
        queue.EnsureDeliveryComplete();

        queue.IsTerminated.Should().BeTrue();

        // Further enqueues should be ignored
        EnqueueAndDeliver(queue, "B");
        observer.Items.Should().Equal("A");
    }

    [Fact]
    public void EnsureDeliveryCompleteClearsPendingItems()
    {
        var observer = new ListObserver<string>();
        var deliveryCount = 0;
        DeliveryQueue<string>? q = null;

        var blockingObserver = new DelegateObserver<string>(item =>
        {
            observer.OnNext(item);
            if (++deliveryCount == 1)
            {
                // While delivering first item, enqueue more then terminate
                using (var scope = q!.AcquireLock())
                {
                    scope.Enqueue("B");
                    scope.Enqueue("C");
                }

                q!.EnsureDeliveryComplete(); // re-entrant — should not spin
            }
        });

        var queue = new DeliveryQueue<string>(_gate, blockingObserver);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        // Only "A" should be delivered — "B" and "C" were cleared by EnsureDeliveryComplete
        observer.Items.Should().Equal("A");
        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void EnsureDeliveryCompleteFromDrainThreadDoesNotDeadlock()
    {
        var observer = new ListObserver<string>();
        DeliveryQueue<string>? q = null;

        var terminatingObserver = new DelegateObserver<string>(_ =>
        {
            // Called from drain thread — EnsureDeliveryComplete must detect
            // re-entrancy via _drainThreadId and skip the spin-wait
            q!.EnsureDeliveryComplete();
        });

        var queue = new DeliveryQueue<string>(_gate, terminatingObserver);
        q = queue;

        // This should NOT deadlock
        var completed = Task.Run(() => EnqueueAndDeliver(queue, "A"));
        var finished = Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(5))).Result;
        finished.Should().BeSameAs(completed, "EnsureDeliveryComplete from drain thread should not deadlock");
    }

    [Fact]
    public async Task EnsureDeliveryCompleteWaitsForInFlightDelivery()
    {
        var observer = new ListObserver<int>();
        using var deliveryStarted = new ManualResetEventSlim(false);
        using var allowDeliveryToFinish = new ManualResetEventSlim(false);

        var slowObserver = new DelegateObserver<int>(item =>
        {
            observer.OnNext(item);
            deliveryStarted.Set();
            allowDeliveryToFinish.Wait();
        });

        var queue = new DeliveryQueue<int>(_gate, slowObserver);

        // Start delivering — will block in observer
        var deliverTask = Task.Run(() => EnqueueAndDeliver(queue, 42));
        deliveryStarted.Wait();

        // Drain thread is blocked in observer callback. EnsureDeliveryComplete should spin.
        var terminateTask = Task.Run(() => queue.EnsureDeliveryComplete());

        // Give terminate a moment to enter spin-wait
        await Task.Delay(100);
        terminateTask.IsCompleted.Should().BeFalse("should be spinning waiting for delivery");

        // Release the delivery
        allowDeliveryToFinish.Set();

        await Task.WhenAll(deliverTask, terminateTask);
        queue.IsTerminated.Should().BeTrue();
        observer.Items.Should().Equal(42);
    }

    [Fact]
    public void TerminalItemsDeliveredBeforeTermination()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.Enqueue("A");
            scope.Enqueue("B");
            scope.EnqueueCompleted();
            scope.Enqueue("C"); // should be ignored — after terminal
        }

        observer.Items.Should().Equal("A", "B");
        observer.IsCompleted.Should().BeTrue();
        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void ErrorTerminatesAndClearsPending()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        var error = new InvalidOperationException("test");

        using (var scope = queue.AcquireLock())
        {
            scope.Enqueue("A");
            scope.EnqueueError(error);
            scope.Enqueue("B"); // should be ignored
        }

        observer.Items.Should().Equal("A");
        observer.Error.Should().BeSameAs(error);
        queue.IsTerminated.Should().BeTrue();
    }
}