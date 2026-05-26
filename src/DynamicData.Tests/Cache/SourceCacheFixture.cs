using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class SourceCacheFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public SourceCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().AsAggregator();
    }

    [Fact]
    public void CanHandleABatchOfUpdates()
    {
        _source.Edit(
            updater =>
            {
                var torequery = new Person("Adult1", 44);

                updater.AddOrUpdate(new Person("Adult1", 40));
                updater.AddOrUpdate(new Person("Adult1", 41));
                updater.AddOrUpdate(new Person("Adult1", 42));
                updater.AddOrUpdate(new Person("Adult1", 43));
                updater.Refresh(torequery);
                updater.Remove(torequery);
                updater.Refresh(torequery);
            });

        _results.Summary.Overall.Count.Should().Be(6, "Should be  6 up`dates");
        _results.Messages.Count.Should().Be(1, "Should be 1 message");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 update");
        _results.Messages[0].Updates.Should().Be(3, "Should be 3 updates");
        _results.Messages[0].Removes.Should().Be(1, "Should be  1 remove");
        _results.Messages[0].Refreshes.Should().Be(1, "Should be 1 evaluate");

        _results.Data.Count.Should().Be(0, "Should be 1 item in` the cache");
    }

    [Fact]
    public void CountChanged()
    {
        var count = 0;
        var invoked = 0;
        using (_source.CountChanged.Subscribe(
                   c =>
                   {
                       count = c;
                       invoked++;
                   }))
        {
            invoked.Should().Be(1);
            count.Should().Be(0);

            _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
            invoked.Should().Be(2);
            count.Should().Be(100);

            _source.Clear();
            invoked.Should().Be(3);
            count.Should().Be(0);
        }
    }

    [Fact]
    public void CountChangedShouldAlwaysInvokeUponeSubscription()
    {
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        result.HasValue.Should().BeTrue();

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        result.Value.Should().Be(0, "Count should be zero");

        subscription.Dispose();
    }

    [Fact]
    public void CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
    {
        var generator = new RandomPersonGenerator();
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        _source.AddOrUpdate(generator.Take(100));

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(100, "Count should be 100");
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void SubscribesDisposesCorrectly()
    {
        var called = false;
        var errored = false;
        var completed = false;
        var subscription = _source.Connect().Finally(() => completed = true).Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
        _source.AddOrUpdate(new Person("Adult1", 40));

        subscription.Dispose();
        _source.Dispose();

        errored.Should().BeFalse();
        called.Should().BeTrue();
        completed.Should().BeTrue();
    }

    [Fact]
    public void EmptyChanges()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(suppressEmptyChangeSets: false)
            .Subscribe(c=> change = c);

        change.Should().NotBeNull();
        change!.Count.Should().Be(0);

    }

    [Fact]
    public void EmptyChangesWithFilter()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(p=>p.Age == 20, suppressEmptyChangeSets: false)
            .Subscribe(c => change = c);

        change.Should().NotBeNull();
        change!.Count.Should().Be(0);
    }



    [Fact]
    public void StaticFilterRemove()
    {
        var cache = new SourceCache<SomeObject, int>(x => x.Id);
        
        var above5 = cache.Connect(x => x.Value > 5).AsObservableCache();
        var below5 = cache.Connect(x => x.Value <= 5).AsObservableCache();

        cache.AddOrUpdate(Enumerable.Range(1,10).Select(i=> new SomeObject(i,i)));


        above5.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5).Select(i => new SomeObject(i, i)));
        below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(i => new SomeObject(i, i)));

        //should move from above 5 to below 5
        cache.AddOrUpdate(new SomeObject(6,-1));

        above5.Count.Should().Be(4);
        below5.Count.Should().Be(6);


        above5.Items.Should().BeEquivalentTo(Enumerable.Range(7, 4).Select(i => new SomeObject(i, i)));
        below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 6).Select(i => new SomeObject(i, i == 6 ? -1 : i)));
    }

    public record class SomeObject(int Id, int Value);


    [Fact]
    public async Task MultiCacheFanInDoesNotDeadlock()
    {
        const int itemCount = 100;

        using var cacheA = new SourceCache<TestItem, string>(static x => x.Key);
        using var cacheB = new SourceCache<TestItem, string>(static x => x.Key);
        using var destination = new SourceCache<TestItem, string>(static x => x.Key);
        using var subA = cacheA.Connect().PopulateInto(destination);
        using var subB = cacheB.Connect().PopulateInto(destination);
        using var results = destination.Connect().AsAggregator();

        var taskA = Task.Run(() =>
        {
            for (var i = 0; i < itemCount; i++)
            {
                cacheA.AddOrUpdate(new TestItem($"a-{i}", $"ValueA-{i}"));
            }
        });

        var taskB = Task.Run(() =>
        {
            for (var i = 0; i < itemCount; i++)
            {
                cacheB.AddOrUpdate(new TestItem($"b-{i}", $"ValueB-{i}"));
            }
        });

        var completed = Task.WhenAll(taskA, taskB);
        var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(10)));

        finished.Should().BeSameAs(completed, "concurrent edits with cross-cache subscribers should not deadlock");
        results.Error.Should().BeNull();
        results.Data.Count.Should().Be(itemCount * 2, "all items from both caches should arrive in the destination");
        results.Data.Items.Should().BeEquivalentTo([.. cacheA.Items, .. cacheB.Items], "all items should be in the destination");
    }

    [Fact]
    public async Task DirectCrossWriteDoesNotDeadlock()
    {
        const int iterations = 50;

        for (var iter = 0; iter < iterations; iter++)
        {
            using var cacheA = new SourceCache<TestItem, string>(static x => x.Key);
            using var cacheB = new SourceCache<TestItem, string>(static x => x.Key);

            // Bidirectional: A items flow into B, B items flow into A.
            // Filter by prefix prevents infinite feedback.
            using var aToB = cacheA.Connect()
                .Filter(static x => x.Key.StartsWith('a'))
                .Transform(static (item, _) => new TestItem("from-a-" + item.Key, item.Value))
                .PopulateInto(cacheB);

            using var bToA = cacheB.Connect()
                .Filter(static x => x.Key.StartsWith('b'))
                .Transform(static (item, _) => new TestItem("from-b-" + item.Key, item.Value))
                .PopulateInto(cacheA);

            using var barrier = new Barrier(2);

            var taskA = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    cacheA.AddOrUpdate(new TestItem("a" + i, "V" + i));
                }
            });

            var taskB = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    cacheB.AddOrUpdate(new TestItem("b" + i, "V" + i));
                }
            });

            var completed = Task.WhenAll(taskA, taskB);
            var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(60)));

            finished.Should().BeSameAs(completed, $"iteration {iter}: bidirectional cross-cache writes should not deadlock");
        }
    }

    [Fact]
    public void ConnectDuringDeliveryDoesNotDuplicate()
    {
        // Exploits the dequeue-to-OnNext window. Thread A writes two items in
        // separate batches. The first delivery is held by a slow subscriber.
        // While item1 delivery is blocked, item2 is committed to ReaderWriter
        // and sitting in the queue. Thread B calls Connect(), takes a snapshot
        // (sees both items), subscribes to _changes, then item2 is delivered
        // via OnNext — producing a duplicate if not guarded by a generation counter.
        using var cache = new SourceCache<TestItem, string>(static x => x.Key);

        using var delivering = new ManualResetEventSlim(false);
        using var item2Written = new ManualResetEventSlim(false);
        using var connectDone = new ManualResetEventSlim(false);

        var firstDelivery = true;

        // First subscriber: blocks on the first delivery to create the window
        using var slowSub = cache.Connect().Subscribe(_ =>
        {
            if (firstDelivery)
            {
                firstDelivery = false;
                delivering.Set();

                // Wait until item2 has been written and the Connect has subscribed
                connectDone.Wait(TimeSpan.FromSeconds(5));
            }
        });

        // Write item1 on a background thread — delivery starts, slow subscriber blocks
        var writeTask = Task.Run(() =>
        {
            cache.AddOrUpdate(new TestItem("k1", "v1"));
        });

        // Wait for delivery of item1 to be in progress (slow sub is blocking)
        delivering.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("delivery should have started");

        // Now write item2 on another thread. It will acquire the lock, commit to
        // ReaderWriter, enqueue a notification, and return. The notification sits
        // in the queue because the deliverer (Thread A) is blocked by the slow sub.
        var writeTask2 = Task.Run(() =>
        {
            cache.AddOrUpdate(new TestItem("k2", "v2"));
            item2Written.Set();
        });
        item2Written.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("item2 should have been written");

        // Now Connect on the main thread. The snapshot from ReaderWriter includes
        // BOTH k1 and k2. The subscription to _changes is added. When the slow
        // subscriber unblocks, item2's notification will be delivered via OnNext
        // and the new subscriber will see k2 again — a duplicate Add.
        var addCounts = new Dictionary<string, int>();
        using var newSub = cache.Connect().Subscribe(changes =>
        {
            foreach (var c in changes)
            {
                if (c.Reason == ChangeReason.Add)
                {
                    var key = c.Current.Key;
                    addCounts[key] = addCounts.GetValueOrDefault(key) + 1;
                }
            }
        });

        // Unblock the slow subscriber — delivery resumes, item2 delivered
        connectDone.Set();
        writeTask.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("writeTask should complete");
        writeTask2.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("writeTask2 should complete");

        // Each key should appear exactly once in the new subscriber's view
        addCounts.GetValueOrDefault("k1").Should().Be(1, "k1 should appear once (snapshot only)");
        addCounts.GetValueOrDefault("k2").Should().Be(1, "k2 should appear once, not duplicated from snapshot + queued delivery");
    }

    private sealed record TestItem(string Key, string Value);
}
