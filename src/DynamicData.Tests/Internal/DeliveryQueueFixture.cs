using System.Collections.Concurrent;

namespace DynamicData.Tests.Internal;

public class DeliveryQueueFixture
{
    private readonly Lock _gate = new();

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
        where T : notnull
    {
        using var scope = queue.AcquireLock();
        scope.EnqueueNext(item);
    }

    private static void TriggerDelivery<T>(DeliveryQueue<T> queue)
        where T : notnull
    {
        using var scope = queue.AcquireLock();
    }

    [Test]
    public async Task EnqueueAndDeliverDeliversItem()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        EnqueueAndDeliver(queue, "A");

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DeliverDeliversItemsInFifoOrder()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueNext("A");
            scope.EnqueueNext("B");
            scope.EnqueueNext("C");
        }

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A", "B", "C" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DeliverWithEmptyQueueIsNoOp()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        TriggerDelivery(queue);

        await Assert.That(observer.Items).IsEmpty();
    }

    [Test]
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

        await Assert.That(maxConcurrent).IsEqualTo(1);
        await Assert.That(delivered).HasCount(101);
    }

    [Test]
    public async Task SecondWriterItemPickedUpByFirstDeliverer()
    {
        var observer = new ListObserver<string>();
        DeliveryQueue<string>? q = null;

        var enqueuingObserver = new DelegateObserver<string>(item =>
        {
            observer.OnNext(item);
            if (observer.Items.Count == 1)
            {
                using var scope = q!.AcquireLock();
                scope.EnqueueNext("B");
            }
        });

        var queue = new DeliveryQueue<string>(_gate, enqueuingObserver);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A", "B" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ReentrantEnqueueDoesNotRecurse()
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
                scope.EnqueueNext("B");
            }

            callDepth--;
        });

        var queue = new DeliveryQueue<string>(_gate, observer);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        await Assert.That(delivered).IsEquivalentTo(new[] { "A", "B" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(maxDepth).IsEqualTo(1);
    }

    [Test]
    public async Task ExceptionInDeliveryResetsDeliveryToken()
    {
        var callCount = 0;
        var observer = new DelegateObserver<string>(_ =>
        {
            if (++callCount == 1)
                throw new InvalidOperationException("boom");
        });

        var queue = new DeliveryQueue<string>(_gate, observer);

        var act = () => EnqueueAndDeliver(queue, "A");
        await Assert.That(act).Throws<InvalidOperationException>();

        EnqueueAndDeliver(queue, "B");

        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task RemainingItemsDeliveredAfterExceptionRecovery()
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
            scope.EnqueueNext("A");
            scope.EnqueueNext("B");
        };

        await Assert.That(act).Throws<InvalidOperationException>();

        shouldThrow = false;
        TriggerDelivery(queue);

        await Assert.That(delivered).IsEquivalentTo(new[] { "B" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TerminalCompletedStopsDelivery()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueNext("A");
            scope.EnqueueCompleted();
            scope.EnqueueNext("B"); // should be ignored after terminal
        }

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(observer.IsCompleted).IsTrue();
        await Assert.That(queue.IsTerminated).IsTrue();
    }

    [Test]
    public async Task TerminalErrorStopsDelivery()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        var error = new InvalidOperationException("test");

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueNext("A");
            scope.EnqueueError(error);
            scope.EnqueueNext("B"); // should be ignored after terminal
        }

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(observer.Error).IsSameReferenceAs(error);
        await Assert.That(queue.IsTerminated).IsTrue();
    }

    [Test]
    public async Task EnqueueAfterTerminationIsIgnored()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueCompleted();
        }

        EnqueueAndDeliver(queue, "AFTER");

        await Assert.That(observer.Items).IsEmpty();
    }

    [Test]
    public async Task IsTerminatedIsFalseInitially()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        await Assert.That(queue.IsTerminated).IsFalse();
    }

    [Test]
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

        await Assert.That(observer.Items.Count).IsEqualTo(threadCount * itemsPerThread);
    }

    [Test]
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

        await Assert.That(observer.Items.Distinct().Count()).IsEqualTo(threadCount * itemsPerThread);
    }

    [Test]
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
            await Assert.That(sequences).IsInOrder();
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

    [Test]
    public async Task DisposeTerminatesQueue()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        EnqueueAndDeliver(queue, "A");
        queue.Dispose();

        await Assert.That(queue.IsTerminated).IsTrue();

        // Further enqueues should be ignored
        EnqueueAndDeliver(queue, "B");
        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DisposeClearsPendingItems()
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
                    scope.EnqueueNext("B");
                    scope.EnqueueNext("C");
                }

                q!.Dispose(); // re-entrant — should not spin
            }
        });

        var queue = new DeliveryQueue<string>(_gate, blockingObserver);
        q = queue;

        EnqueueAndDeliver(queue, "A");

        // Only "A" should be delivered — "B" and "C" were cleared by Dispose
        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(queue.IsTerminated).IsTrue();
    }

    [Test]
    public async Task DisposeFromDrainThreadDoesNotDeadlock()
    {
        var observer = new ListObserver<string>();
        DeliveryQueue<string>? q = null;

        var terminatingObserver = new DelegateObserver<string>(_ =>
        {
            // Called from drain thread — Dispose must detect
            // re-entrancy via _drainThreadId and skip the spin-wait
            q!.Dispose();
        });

        var queue = new DeliveryQueue<string>(_gate, terminatingObserver);
        q = queue;

        // This should NOT deadlock
        var completed = Task.Run(() => EnqueueAndDeliver(queue, "A"));
        var finished = Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(5))).Result;
        await Assert.That(finished).IsSameReferenceAs(completed);
    }

    [Test]
    public async Task DisposeWaitsForInFlightDelivery()
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

        // Drain thread is blocked in observer callback. Dispose should spin.
        var terminateTask = Task.Run(() => queue.Dispose());

        // Give terminate a moment to enter spin-wait
        await Task.Delay(100);
        await Assert.That(terminateTask.IsCompleted).IsFalse();

        // Release the delivery
        allowDeliveryToFinish.Set();

        await Task.WhenAll(deliverTask, terminateTask);
        await Assert.That(queue.IsTerminated).IsTrue();
        await Assert.That(observer.Items).IsEquivalentTo(new[] { 42 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TerminalItemsDeliveredBeforeTermination()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueNext("A");
            scope.EnqueueNext("B");
            scope.EnqueueCompleted();
            scope.EnqueueNext("C"); // should be ignored — after terminal
        }

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A", "B" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(observer.IsCompleted).IsTrue();
        await Assert.That(queue.IsTerminated).IsTrue();
    }

    [Test]
    public async Task ErrorTerminatesAndClearsPending()
    {
        var observer = new ListObserver<string>();
        var queue = new DeliveryQueue<string>(_gate, observer);
        var error = new InvalidOperationException("test");

        using (var scope = queue.AcquireLock())
        {
            scope.EnqueueNext("A");
            scope.EnqueueError(error);
            scope.EnqueueNext("B"); // should be ignored
        }

        await Assert.That(observer.Items).IsEquivalentTo(new[] { "A" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(observer.Error).IsSameReferenceAs(error);
        await Assert.That(queue.IsTerminated).IsTrue();
    }
}
