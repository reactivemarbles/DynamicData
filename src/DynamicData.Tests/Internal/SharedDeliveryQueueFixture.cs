// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

public class SharedDeliveryQueueFixture
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    [Fact]
    public void SingleSourceDeliversItems()
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

        delivered.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void MultipleSourcesSerializeDelivery()
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

        delivered.Should().Equal("int:1", "str:hello");
    }

    [Fact]
    public void ErrorTerminatesAllSubQueues()
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

        queue.IsTerminated.Should().BeTrue();

        // Further enqueues should be ignored
        using (var scope2 = sub2.AcquireLock())
        {
            scope2.EnqueueNext("ignored");
        }

        delivered1.Should().Equal(1);
        obs1.Error.Should().NotBeNull();
        delivered2.Should().BeEmpty();
    }

    [Fact]
    public void CompletionDoesNotTerminateParent()
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

        queue.IsTerminated.Should().BeFalse("completion of one sub-queue should not terminate parent");
        obs1.IsCompleted.Should().BeTrue();

        // Other sub-queue should still work
        using (var scope2 = sub2.AcquireLock())
        {
            scope2.EnqueueNext("still alive");
        }

        delivered2.Should().Equal("still alive");
    }

    [Fact]
    public void DisposeTerminatesAndWaits()
    {
        var queue = new SharedDeliveryQueue(_gate);
        var observer = new TestObserver<int>(_ => { });
        var sub = queue.CreateQueue(observer);

        using (var scope = sub.AcquireLock())
        {
            scope.EnqueueNext(1);
        }

        queue.Dispose();

        queue.IsTerminated.Should().BeTrue();
    }

    [Fact]
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

        delivered.Count.Should().Be(threadCount * itemsPerThread);

        // Each thread's items should all be present
        for (var t = 0; t < threadCount; t++)
        {
            var threadItems = delivered.Where(s => s.StartsWith($"{t}:")).Count();
            threadItems.Should().Be(itemsPerThread);
        }
    }

    /// <summary>
    /// Regression test for the C1 fix: when <c>_onDrainComplete</c> enqueues an item onto a
    /// sub-queue (modeling an orchestrator emitting accumulated state), the reentrant drain
    /// inside <c>ExitLockAndDrain</c> processes the item but leaves <c>_activeBits</c> empty.
    /// Without the fix, the outer <c>DrainAll</c> exits without re-firing <c>_onDrainComplete</c>;
    /// any state the orchestrator accumulates during the reentrant drain is then stranded until
    /// the next unrelated drain. The fix flags drain reentrancy and loops <c>_onDrainComplete</c>
    /// when the flag is set, even when <c>_activeBits</c> shows nothing pending.
    /// </summary>
    [Fact]
    public void OnDrainComplete_RefiresAfterReentrantDrainTriggeredByCallback()
    {
        var callCount = 0;
        var delivered = new List<int>();
        DeliverySubQueue<int>? sub = null;

        SharedDeliveryQueue queue = null!;
        queue = new SharedDeliveryQueue(onDrainComplete: _ =>
        {
            // Emit one item on the first callback invocation only. The enqueue happens while we
            // are inside DrainAll on the drain thread, so ExitLockAndDrain takes the reentrant
            // path and consumes the item without setting _activeBits. The outer DrainAll must
            // still re-fire OnDrainComplete because of the reentrancy flag.
            if (Interlocked.Increment(ref callCount) == 1)
            {
                using var scope = sub!.AcquireLock();
                scope.EnqueueNext(42);
            }
        });

        var observer = new TestObserver<int>(delivered.Add);
        sub = queue.CreateQueue(observer);

        // Prime the drain with an initial item so DrainAll fires onDrainComplete at least once.
        using (var scope = sub.AcquireLock())
        {
            scope.EnqueueNext(1);
        }

        delivered.Should().Equal(new[] { 1, 42 }, "the reentrantly-enqueued item must be delivered");
        callCount.Should().BeGreaterThanOrEqualTo(2,
            "onDrainComplete must fire again after the reentrant drain consumed the enqueued item");
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