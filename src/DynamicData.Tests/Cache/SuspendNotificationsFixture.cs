#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif

namespace DynamicData.Tests.Cache;

[NotInParallel]
public sealed class SuspendNotificationsRegressionFixture : IDisposable
{
    private readonly SourceCache<int, int> _source = new(static x => x);

    private readonly ChangeSetAggregator<int, int> _results;

    private readonly List<int> _countChangeHistory = [];

    private readonly IDisposable _countChangeSubscription;

    public SuspendNotificationsRegressionFixture()
    {
        _results = _source.Connect().AsAggregator();
        _countChangeSubscription = _source.CountChanged.Do(_countChangeHistory.Add).Subscribe();
    }

    [Test]
    public async Task NotificationsCanBeSuspended()
    {
        // Arrange
        using var suspend = _source.SuspendNotifications();

        // Act
        _source.AddOrUpdate(1);

        // Assert
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no item updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should not receive data after suspend");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task SuspendingNotificationsDoesNotImpactPreview()
    {
        // Arrange
        using var previewResults = _source.Preview().AsAggregator();
        using var suspend = _source.SuspendNotifications();

        // Act
        _source.AddOrUpdate(1);

        // Assert
        await Assert.That(previewResults.Messages.Count).IsEqualTo(1).Because("should have received a message in Preview");
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("should not have gotten any updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("should not receive data after suspend");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task SuspendingNotificationsPreventsWatch()
    {
        // Arrange
        var gotData = false;
        using var suspend = _source.SuspendNotifications();
        using var sub = _source.Watch(1).Do(_ => gotData = true).Subscribe();

        // Act
        _source.AddOrUpdate(1);

        // Assert
        await Assert.That(gotData).IsFalse().Because("Should not have received data after suspend");
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no item updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should not receive data after suspend");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task NotificationsCanBeResumed()
    {
        // Arrange
        {
            using var suspend = _source.SuspendNotifications();
        }

        // Act
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);

        // Assert
        await Assert.That(_results.Messages.Count).IsEqualTo(37).Because("Should receive updates after resume");
        await Assert.That(_results.Data.Count).IsEqualTo(37).Because("Should receive data after resume");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task ExistingDataNotEmittedWhileSuspended()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);

        // Act
        using var results = _source.Connect().AsAggregator();

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("Should have no item updates");
        await Assert.That(results.Data.Count).IsEqualTo(0).Because("Should not receive data after suspend");
        await Assert.That(results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task ExistingDataNotEmittedViaWatchUntilResumed()
    {
        // Arrange
        var gotData = false;
        var suspend = _source.SuspendNotifications();
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
        using var sub = _source.Watch(1).Do(_ => gotData = true).Subscribe();

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(gotData).IsTrue().Because("should have received a notice after the suspend was released");
    }

    [Test]
    public async Task ExistingDataNotEmittedUntilResumed()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
        using var results = _source.Connect().AsAggregator();

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("Should receive updates after resume");
        await Assert.That(results.Data.Count).IsEqualTo(37).Because("Should receive data after resume");
        await Assert.That(results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task ExistingAndNewDataEmittedAsASingleChangesetOnResume()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
        using var results = _source.Connect().AsAggregator();
        Enumerable.Range(101, 37).ForEach(_source.AddOrUpdate);

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("Should receive single changeset on resume");
        await Assert.That(results.Data.Count).IsEqualTo(37 * 2).Because("Should receive data after resume");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task PendingNotificationsEmittedAsSingleChangeSetOnResume()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
        _source.RemoveKey(1);

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(36).Because("Should receive data after resume");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should receive single changeset on resume");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(37).Because("Should have 37 adds");
        await Assert.That(_results.Messages[0].Removes).IsEqualTo(1).Because("Should show the remove");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task MultipleSuspendsAreCumulative()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        using var suspend2 = _source.SuspendNotifications();
        _source.AddOrUpdate(1);

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no item updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should not receive data after suspend");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task MultipleSuspendsCanBeResumed()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        var suspend2 = _source.SuspendNotifications();
        _source.AddOrUpdate(1);
        suspend.Dispose();

        // Act
        suspend2.Dispose();

        // Assert
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should receive updates after resume");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should receive data after resume");
        await Assert.That(_results.IsCompleted).IsFalse().Because("IsCompleted should not have fired");
    }

    [Test]
    public async Task OnCompletedFiresIfCacheDisposedWhileSuspended()
    {
        // Arrange
        using var suspend = _source.SuspendNotifications();
        using var results = _source.Connect().AsAggregator();
        Enumerable.Range(101, 37).ForEach(_source.AddOrUpdate);

        // Act
        _source.Dispose();

        // Assert
        await Assert.That(results.IsCompleted).IsTrue().Because("IsCompleted should fire even if Notifications are suspended");
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("Shouldn't receive any Changesets");
        await Assert.That(results.Data.Count).IsEqualTo(0).Because("Shouldn't receive any Data");
    }

    [Test]
    public async Task CountNotificationsCanBeSuspended()
    {
        // Arrange
        using var suspend = _source.SuspendCount();

        // Act
        _source.AddOrUpdate(1);

        // Assert
        await Assert.That(_countChangeHistory.Count).IsEqualTo(1).Because("Should Not receive count updates");
        await Assert.That(_countChangeHistory[0]).IsEqualTo(0).Because("Should have only received the empty list");
    }

    [Test]
    public async Task CountNotificationsCanBeResumed()
    {
        // Arrange
        {
            using var suspend = _source.SuspendCount();
        }

        // Act
        _source.AddOrUpdate(1);

        // Assert
        await Assert.That(_countChangeHistory.Count).IsEqualTo(2).Because("Should receive count updates");
        await Assert.That(_countChangeHistory[0]).IsEqualTo(0).Because("Should have received the empty list");
        await Assert.That(_countChangeHistory[1]).IsEqualTo(1).Because("Should have received the updated count");
    }

    [Test]
    public async Task CountChangedAlwaysStartsWithInitialEvenWhenSuspended()
    {
        // Arrange
        _source.AddOrUpdate(Enumerable.Range(1, 50));
        var countChangeHistory = new List<int>();
        using var suspend = _source.SuspendCount();
        using var countChangeSubscription = _source.CountChanged.Do(countChangeHistory.Add).Subscribe();

        // Act
        Enumerable.Range(100, 50).ForEach(_source.AddOrUpdate);

        // Assert
        await Assert.That(countChangeHistory.Count).IsEqualTo(1).Because("Should receive initial value");
        await Assert.That(countChangeHistory[0]).IsEqualTo(50).Because("Should have received the correct initial value");
    }

    [Test]
    public async Task PendingCountNotificationsEmittedOnResume()
    {
        // Arrange
        var suspend = _source.SuspendCount();
        _source.AddOrUpdate(1);
        _source.AddOrUpdate(2);
        _source.AddOrUpdate(3);

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(_countChangeHistory.Count).IsEqualTo(2).Because("Should receive count updates");
        await Assert.That(_countChangeHistory[0]).IsEqualTo(0).Because("Should have received the initial 0 count");
        await Assert.That(_countChangeHistory[1]).IsEqualTo(3).Because("Should have received the updated count");
    }

    [Test]
    public async Task MultipleCountSuspendsAreCumulative()
    {
        // Arrange
        var suspend = _source.SuspendCount();
        using var suspend2 = _source.SuspendCount();
        _source.AddOrUpdate(1);
        _source.AddOrUpdate(2);
        _source.AddOrUpdate(3);

        // Act
        suspend.Dispose();

        // Assert
        await Assert.That(_countChangeHistory.Count).IsEqualTo(1).Because("Should Not receive count updates");
        await Assert.That(_countChangeHistory[0]).IsEqualTo(0).Because("Should have only received the empty list");
    }

    [Test]
    public async Task MultipleCountSuspendsCanBeResumed()
    {
        // Arrange
        var suspend = _source.SuspendCount();
        var suspend2 = _source.SuspendCount();
        _source.AddOrUpdate(1);
        _source.AddOrUpdate(2);
        _source.AddOrUpdate(3);
        suspend.Dispose();

        // Act
        suspend2.Dispose();

        // Assert
        await Assert.That(_countChangeHistory.Count).IsEqualTo(2).Because("Should receive count updates");
        await Assert.That(_countChangeHistory[0]).IsEqualTo(0).Because("Should have received the initial 0 count");
        await Assert.That(_countChangeHistory[1]).IsEqualTo(3).Because("Should have received the updated count");
    }

    [Test]
    public async Task SuspensionsAreThreadSafe()
    {
        // Arrange
        var suspend = _source.SuspendNotifications();
        var tasks = Enumerable.Range(1, 100).Select(x => Task.Run(() => _source.AddOrUpdate(x))).ToArray();
        await Task.WhenAll(tasks);

        // Act
        await Task.Run(suspend.Dispose);

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should receive data after resume");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should receive single changeset on resume");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should have 100 adds");
    }

    [Test]
    public async Task ResumeThenReSuspendDeliversFirstBatchOnly()
    {
        // Forces the ordering: resume completes before re-suspend.
        // The deferred subscriber activates with the first batch snapshot,
        // then re-suspend holds the second batch until final resume.
        using var cache = new SourceCache<int, int>(static x => x);
        var dataSet1 = Enumerable.Range(0, 100).ToList();
        var dataSet2 = Enumerable.Range(1000, 100).ToList();
        var allData = dataSet1.Concat(dataSet2).ToList();

        var suspend1 = cache.SuspendNotifications();
        cache.AddOrUpdate(dataSet1);

        using var results = cache.Connect().AsAggregator();
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("no messages during suspension");

        // Resume first — subscriber activates
        suspend1.Dispose();

        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("exactly one message after resume");
        await Assert.That(results.Messages[0].Adds).IsEqualTo(dataSet1.Count).Because($"snapshot should have {dataSet1.Count} adds");
        await Assert.That(results.Messages[0].Removes).IsEqualTo(0).Because("no removes");
        await Assert.That(results.Messages[0].Updates).IsEqualTo(0).Because("no updates");
        await Assert.That(results.Messages[0].Select(x => x.Key)).IsEquivalentTo(dataSet1).Because("snapshot should contain first batch keys");

        // Re-suspend, write second batch
        var suspend2 = cache.SuspendNotifications();
        cache.AddOrUpdate(dataSet2);

        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("still one message — second batch held by suspension");
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(dataSet1.Count).Because($"still {dataSet1.Count} adds total");

        // Final resume
        suspend2.Dispose();

        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("two messages total");
        await Assert.That(results.Messages[1].Adds).IsEqualTo(dataSet2.Count).Because($"second message has {dataSet2.Count} adds");
        await Assert.That(results.Messages[1].Removes).IsEqualTo(0).Because("no removes in second message");
        await Assert.That(results.Messages[1].Updates).IsEqualTo(0).Because("no updates in second message");
        await Assert.That(results.Messages[1].Select(x => x.Key)).IsEquivalentTo(dataSet2).Because("second message should contain second batch keys");

        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(allData.Count).Because($"exactly {allData.Count} adds total");
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0).Because("no removes");
        await Assert.That(results.Data.Count).IsEqualTo(allData.Count).Because($"{allData.Count} items in final state");
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.IsCompleted).IsFalse();
    }

    [Test]
    public async Task ReSuspendThenResumeDeliversAllInSingleBatch()
    {
        // Forces the ordering: re-suspend before resume.
        // Suspend count goes 1→2→1, no resume signal fires.
        // Both batches accumulate and arrive as a single changeset on final resume.
        using var cache = new SourceCache<int, int>(static x => x);
        var dataSet1 = Enumerable.Range(0, 100).ToList();
        var dataSet2 = Enumerable.Range(1000, 100).ToList();
        var allData = dataSet1.Concat(dataSet2).ToList();

        var suspend1 = cache.SuspendNotifications();
        cache.AddOrUpdate(dataSet1);

        using var results = cache.Connect().AsAggregator();
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("no messages during suspension");

        // Re-suspend first — count goes 1→2
        var suspend2 = cache.SuspendNotifications();

        // Resume first suspend — count goes 2→1, still suspended
        suspend1.Dispose();

        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("no messages — still suspended (count=1)");
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(0).Because("no adds — still suspended");

        // Write second batch while still suspended
        cache.AddOrUpdate(dataSet2);

        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("still no messages");

        // Final resume — count goes 1→0
        suspend2.Dispose();

        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("single message with all data");
        await Assert.That(results.Messages[0].Adds).IsEqualTo(allData.Count).Because($"all {allData.Count} items in one changeset");
        await Assert.That(results.Messages[0].Removes).IsEqualTo(0).Because("no removes");
        await Assert.That(results.Messages[0].Updates).IsEqualTo(0).Because("no updates");
        await Assert.That(results.Messages[0].Select(c => c.Key).OrderBy(k => k)).IsEquivalentTo(allData).Because("should contain both batches in order");

        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(allData.Count).Because($"exactly {allData.Count} adds total");
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0).Because("no removes");
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0).Because("no updates");
        await Assert.That(results.Data.Count).IsEqualTo(allData.Count).Because($"{allData.Count} items in final state");
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.IsCompleted).IsFalse();
    }

    [Test]
    public async Task ConcurrentSuspendDuringResumeDoesNotCorrupt()
    {
        // Stress test: races resume against re-suspend on two threads.
        // Both orderings are correct (tested deterministically above).
        // This test verifies no corruption, deadlocks, or data loss under contention.
        const int iterations = 200;
        var dataSet1 = Enumerable.Range(0, 100).ToList();
        var dataSet2 = Enumerable.Range(1000, 100).ToList();
        var allData = dataSet1.Concat(dataSet2).ToList();

        for (var iter = 0; iter < iterations; iter++)
        {
            using var cache = new SourceCache<int, int>(static x => x);

            var suspend1 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet1);
            using var results = cache.Connect().AsAggregator();

            using var barrier = new Barrier(2);
            var resumeTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                suspend1.Dispose();
            });

            var reSuspendTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                return cache.SuspendNotifications();
            });

            await Task.WhenAll(resumeTask, reSuspendTask);
            var suspend2 = await reSuspendTask;

            cache.AddOrUpdate(dataSet2);
            suspend2.Dispose();

            await Assert.That(results.Summary.Overall.Adds).IsEqualTo(allData.Count).Because($"iteration {iter}: exactly {allData.Count} adds");
            await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0).Because($"iteration {iter}: no removes");
            await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0).Because($"iteration {iter}: no updates because keys don't overlap");
            await Assert.That(results.Data.Count).IsEqualTo(allData.Count).Because($"iteration {iter}: {allData.Count} items in final state");
            await Assert.That(results.Data.Keys.OrderBy(k => k)).IsEquivalentTo(allData).Because($"iteration {iter}: all keys present in order");
            await Assert.That(results.Error).IsNull().Because($"iteration {iter}: no errors");
            await Assert.That(results.IsCompleted).IsFalse().Because($"iteration {iter}: not completed");
        }
    }

    [Test]
    public async Task ResumeSignalUnderLockPreventsStaleSnapshotFromReSuspend()
    {
        // Verifies that a deferred Connect subscriber never sees data written during
        // a re-suspension. The resume signal fires under the lock (reentrant), so the
        // deferred subscriber activates and takes its snapshot before any other thread
        // can re-suspend or write new data.
        //
        // A slow first subscriber blocks delivery of accumulated changes, creating a
        // window where the main thread re-suspends and writes a second batch. The
        // deferred subscriber's snapshot must contain only the first batch.
        using var cache = new SourceCache<int, int>(static x => x);
        var dataSet1 = Enumerable.Range(0, 100).ToList();
        var dataSet2 = Enumerable.Range(1000, 100).ToList();
        var allData = dataSet1.Concat(dataSet2).ToList();

        using var delivering = new SemaphoreSlim(0, 1);
        using var proceedWithResuspend = new SemaphoreSlim(0, 1);

        var suspend1 = cache.SuspendNotifications();
        cache.AddOrUpdate(dataSet1);

        // First subscriber blocks on delivery to hold the delivery thread
        var firstDelivery = true;
        using var slowSub = cache.Connect().Subscribe(_ =>
        {
            if (firstDelivery)
            {
                firstDelivery = false;
                delivering.Release();
                proceedWithResuspend.Wait(TimeSpan.FromSeconds(5));
            }
        });

        // Deferred subscriber — will activate when resume signal fires
        using var results = cache.Connect().AsAggregator();
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("no messages during suspension");

        // Resume on background thread — delivery blocks on slow subscriber
        var resumeTask = Task.Run(() => suspend1.Dispose());
        await Assert.That((await delivering.WaitAsync(TimeSpan.FromSeconds(5)))).IsTrue().Because("delivery should have started");

        // Re-suspend and write second batch while delivery is blocked
        var suspend2 = cache.SuspendNotifications();
        cache.AddOrUpdate(dataSet2);

        // dataSet2 must not appear in any message received so far
        foreach (var msg in results.Messages)
        {
            foreach (var change in msg)
            {
                await Assert.That(change.Key is >= 0 and <= 99)
                    .IsTrue().Because("deferred subscriber should only have first-batch keys before second resume");
            }
        }

        // Unblock delivery
        proceedWithResuspend.Release();
        await resumeTask;

        // Only dataSet1 should have been delivered — dataSet2 is held by second suspension
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(dataSet1.Count).Because($"exactly {dataSet1.Count} adds before second resume — dataSet2 must be held by suspension");
        await Assert.That(results.Messages).HasCount(1).Because("exactly one message (snapshot of dataSet1)");
        await Assert.That(results.Messages[0].Adds).IsEqualTo(dataSet1.Count);
        await Assert.That(results.Messages[0].Select(c => c.Key)).IsEquivalentTo(dataSet1).Because("snapshot should contain exactly first-batch keys in order");

        // Resume second suspension — dataSet2 arrives now
        suspend2.Dispose();

        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(allData.Count).Because($"exactly {allData.Count} adds total");
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0).Because("no removes");
        await Assert.That(results.Messages).HasCount(2).Because("two messages: snapshot + second batch");
        await Assert.That(results.Messages[1].Adds).IsEqualTo(dataSet2.Count);
        await Assert.That(results.Messages[1].Select(c => c.Key)).IsEquivalentTo(dataSet2).Because("second message should contain exactly second-batch keys in order");
        await Assert.That(results.Data.Count).IsEqualTo(allData.Count);
        await Assert.That(results.Data.Keys.OrderBy(k => k)).IsEquivalentTo(allData);
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.IsCompleted).IsFalse();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
        _countChangeSubscription.Dispose();
    }
}
