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

    [Fact]
    public void ReceiptOrderIsPreservedAcrossSubQueues()
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
        var drainer = Task.Run(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(1);
        });

        firstIsDelivering.Wait(TimeSpan.FromSeconds(5));

        using (var scope = sub1.AcquireLock())
        {
            scope.EnqueueNext(2);
        }

        using (var scope = sub2.AcquireLock())
        {
            scope.EnqueueNext("hello");
        }

        blockFirst.Set();
        drainer.Wait(TimeSpan.FromSeconds(5));

        delivered.Should().Equal(new[] { "int:1", "int:2", "str:hello" }, "delivery should follow the order the notifications were received, not the order the sub-queues were created");
    }

    [Fact]
    public void InterleavedSubQueuesDeliverInReceiptOrder()
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

        var drainer = Task.Run(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(0);
        });

        parked.Wait(TimeSpan.FromSeconds(5));

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
        drainer.Wait(TimeSpan.FromSeconds(5));

        delivered.Should().Equal("int:0", "str:a", "int:2", "str:b", "int:4");
    }

    [Fact]
    public void DisposedSubQueueDoesNotDeliverQueuedItems()
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

        var drainer = Task.Run(() =>
        {
            using var scope = sub1.AcquireLock();
            scope.EnqueueNext(0);
        });

        parked.Wait(TimeSpan.FromSeconds(5));

        using (var scope = sub2.AcquireLock())
        {
            scope.EnqueueNext("dropped");
        }

        sub2.Dispose();

        block.Set();
        drainer.Wait(TimeSpan.FromSeconds(5));

        delivered.Should().Equal(new[] { "int:0" }, "a disposed sub-queue should not deliver what it had queued");
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