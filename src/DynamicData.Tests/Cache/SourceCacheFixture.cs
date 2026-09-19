using DynamicData.Tests.Domain;

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

    [Test]
    public async Task CanHandleABatchOfUpdates()
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

        await Assert.That(_results.Summary.Overall.Count).IsEqualTo(6).Because("Should be  6 up`dates");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 message");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 update");
        await Assert.That(_results.Messages[0].Updates).IsEqualTo(3).Because("Should be 3 updates");
        await Assert.That(_results.Messages[0].Removes).IsEqualTo(1).Because("Should be  1 remove");
        await Assert.That(_results.Messages[0].Refreshes).IsEqualTo(1).Because("Should be 1 evaluate");

        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 1 item in` the cache");
    }

    [Test]
    public async Task CountChanged()
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
            await Assert.That(invoked).IsEqualTo(1);
            await Assert.That(count).IsEqualTo(0);

            _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
            await Assert.That(invoked).IsEqualTo(2);
            await Assert.That(count).IsEqualTo(100);

            _source.Clear();
            await Assert.That(invoked).IsEqualTo(3);
            await Assert.That(count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CountChangedShouldAlwaysInvokeUponeSubscription()
    {
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        await Assert.That(result.HasValue).IsTrue();

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        await Assert.That(result.Value).IsEqualTo(0).Because("Count should be zero");

        subscription.Dispose();
    }

    [Test]
    public async Task CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
    {
        var generator = new RandomPersonGenerator();
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        _source.AddOrUpdate(generator.Take(100));

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        await Assert.That(result.HasValue).IsTrue();
        await Assert.That(result.Value).IsEqualTo(100).Because("Count should be 100");
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task SubscribesDisposesCorrectly()
    {
        var called = false;
        var errored = false;
        var completed = false;
        var subscription = _source.Connect().Finally(() => completed = true).Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
        _source.AddOrUpdate(new Person("Adult1", 40));

        subscription.Dispose();
        _source.Dispose();

        await Assert.That(errored).IsFalse();
        await Assert.That(called).IsTrue();
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task EmptyChanges()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(suppressEmptyChangeSets: false)
            .Subscribe(c => change = c);

        await Assert.That(change).IsNotNull();
        await Assert.That(change!.Count).IsEqualTo(0);

    }

    [Test]
    public async Task EmptyChangesWithFilter()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(p => p.Age == 20, suppressEmptyChangeSets: false)
            .Subscribe(c => change = c);

        await Assert.That(change).IsNotNull();
        await Assert.That(change!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task StaticFilterRemove()
    {
        var cache = new SourceCache<SomeObject, int>(x => x.Id);

        var above5 = cache.Connect(x => x.Value > 5).AsObservableCache();
        var below5 = cache.Connect(x => x.Value <= 5).AsObservableCache();

        cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new SomeObject(i, i)));

        await Assert.That(above5.Items).IsEquivalentTo(Enumerable.Range(6, 5).Select(i => new SomeObject(i, i)));
        await Assert.That(below5.Items).IsEquivalentTo(Enumerable.Range(1, 5).Select(i => new SomeObject(i, i)));

        //should move from above 5 to below 5
        cache.AddOrUpdate(new SomeObject(6, -1));

        await Assert.That(above5.Count).IsEqualTo(4);
        await Assert.That(below5.Count).IsEqualTo(6);

        await Assert.That(above5.Items).IsEquivalentTo(Enumerable.Range(7, 4).Select(i => new SomeObject(i, i)));
        await Assert.That(below5.Items).IsEquivalentTo(Enumerable.Range(1, 6).Select(i => new SomeObject(i, i == 6 ? -1 : i)));
    }

    public record class SomeObject(int Id, int Value);

    [Test]
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

        await Assert.That(finished).IsSameReferenceAs(completed).Because("concurrent edits with cross-cache subscribers should not deadlock");
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Data.Count).IsEqualTo(itemCount * 2).Because("all items from both caches should arrive in the destination");
        await Assert.That(results.Data.Items).IsEquivalentTo([.. cacheA.Items, .. cacheB.Items]).Because("all items should be in the destination");
    }

    [Test]
    [NotInParallel]
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

            await Assert.That(finished).IsSameReferenceAs(completed).Because($"iteration {iter}: bidirectional cross-cache writes should not deadlock");
        }
    }

    [Test]
    public async Task ConnectDuringDeliveryDoesNotDuplicate()
    {
        using var cache = new SourceCache<TestItem, string>(static item => item.Key);
        var delivering = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var connectDone = new ManualResetEventSlim(false);
        var firstDelivery = true;
        using var slowSubscription = cache.Connect().Subscribe(_ =>
        {
            if (!firstDelivery)
                return;

            firstDelivery = false;
            delivering.TrySetResult();
            if (!connectDone.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The second subscriber did not connect during delivery.");
        });

        // This writer intentionally blocks inside OnNext. Give it its own thread
        // so it cannot starve the pool that runs the test's async continuations.
        var firstWrite = Task.Factory.StartNew(
            () => cache.AddOrUpdate(new TestItem("k1", "v1")),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        try
        {
            await delivering.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Run(() => cache.AddOrUpdate(new TestItem("k2", "v2")))
                .WaitAsync(TimeSpan.FromSeconds(15));

            // Both writes are committed, but the second delivery is still queued.
            var addCounts = new Dictionary<string, int>();
            using var newSubscription = cache.Connect().Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    if (change.Reason == ChangeReason.Add)
                        addCounts[change.Key] = addCounts.GetValueOrDefault(change.Key) + 1;
                }
            });

            connectDone.Set();
            await firstWrite.WaitAsync(TimeSpan.FromSeconds(15));
            await Assert.That(addCounts.GetValueOrDefault("k1")).IsEqualTo(1);
            await Assert.That(addCounts.GetValueOrDefault("k2")).IsEqualTo(1)
                .Because("the queued update must not duplicate the subscription snapshot");
        }
        finally
        {
            connectDone.Set();
            await firstWrite.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private sealed record TestItem(string Key, string Value);
}
