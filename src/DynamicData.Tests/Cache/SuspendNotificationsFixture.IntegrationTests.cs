using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace DynamicData.Tests.Cache;

public static partial class SuspendNotificationsFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Test]
        public async Task ResumeDeliversPendingChangesWithoutHoldingTheLock()
        {
            // On resume, the changes accumulated while suspended must be delivered to
            // subscribers WITHOUT the cache lock held. A subscriber that blocks mid-delivery
            // must therefore not stall an unrelated, lock-requiring operation on another
            // thread. If the lock were held during delivery, the concurrent operation would
            // block for the full duration of the blocked subscriber.
            //
            // Dedicated threads are used (rather than the thread pool) so the test is immune
            // to pool starvation when run alongside other test collections.
            using var cache = new SourceCache<int, int>(static x => x);
            using var deliveryStarted = new ManualResetEventSlim(false);
            using var releaseDelivery = new ManualResetEventSlim(false);
            using var concurrentOpDone = new ManualResetEventSlim(false);

            // An active subscriber (connected before suspension) that blocks on its first delivery.
            var blockOnce = true;
            using var slowSub = cache.Connect().Subscribe(_ =>
            {
                if (blockOnce)
                {
                    blockOnce = false;
                    deliveryStarted.Set();
                    releaseDelivery.Wait(TimeSpan.FromSeconds(30));
                }
            });

            var suspend = cache.SuspendNotifications();
            cache.AddOrUpdate(Enumerable.Range(0, 10));

            // Resume on a background thread: the accumulated changes are delivered to the
            // slow subscriber, which blocks partway through.
            var resumeThread = new Thread(suspend.Dispose) { IsBackground = true };
            resumeThread.Start();
            await Assert.That(deliveryStarted.Wait(TimeSpan.FromSeconds(10))).IsTrue().Because("delivery of pending changes should have started");

            // While delivery is blocked, a concurrent lock-requiring operation must complete
            // promptly. It cannot if the delivery is happening under the cache lock. The
            // distinguishing gap is large (milliseconds if the lock is free, ~30s if held),
            // so a generous threshold stays reliable under cold-start JIT and heavy load.
            var concurrentThread = new Thread(() =>
            {
                cache.SuspendNotifications().Dispose();
                concurrentOpDone.Set();
            })
            { IsBackground = true };
            concurrentThread.Start();

            var completedWhileBlocked = concurrentOpDone.Wait(TimeSpan.FromSeconds(10));

            releaseDelivery.Set();
            await Assert.That(resumeThread.Join(TimeSpan.FromSeconds(30))).IsTrue().Because("resume should complete");
            await Assert.That(concurrentThread.Join(TimeSpan.FromSeconds(30))).IsTrue().Because("concurrent operation should complete");

            await Assert.That(completedWhileBlocked).IsTrue().Because("a concurrent operation must not block while pending changes are delivered; the lock must not be held during delivery");
            await Assert.That(cache.Count).IsEqualTo(10).Because("all items should be present after resume");
        }

        [Test]
        public async Task StaleResumeSignalIsSuppressedByConcurrentReSuspend()
        {
            // Deterministic reproduction of the suspend/resume state-divergence race (#1131).
            // Resume decrements the suspend count in one step and emits its resume signal in a
            // later step. If a re-suspend slips in between, the resume signal must NOT fire:
            // otherwise the suspended-notification subject would say "resumed" while the count
            // says "suspended", and a connection made during the re-suspension would wrongly
            // activate and receive data while notifications are suspended.
            //
            // The interleaving is forced deterministically: an active subscriber blocks the
            // resume thread inside delivery, after the count has been decremented to zero but
            // before the resume signal, giving the main thread a window to re-suspend and connect.
            using var cache = new SourceCache<int, int>(static x => x);
            var dataSet = Enumerable.Range(0, 50).ToList();

            using var deliveryStarted = new ManualResetEventSlim(false);
            using var releaseDelivery = new ManualResetEventSlim(false);

            var blockOnce = true;
            using var activeSub = cache.Connect().Subscribe(_ =>
            {
                if (blockOnce)
                {
                    blockOnce = false;
                    deliveryStarted.Set();
                    releaseDelivery.Wait(TimeSpan.FromSeconds(30));
                }
            });

            var suspend1 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet);

            // Resume on a background thread: it decrements the count to zero and, while delivering
            // the accumulated changes to the blocking subscriber, parks BEFORE the resume signal.
            var resumeThread = new Thread(suspend1.Dispose) { IsBackground = true };
            resumeThread.Start();
            await Assert.That(deliveryStarted.Wait(TimeSpan.FromSeconds(10))).IsTrue().Because("delivery should have started");

            // The resume thread is parked after decrementing the count but before signalling
            // resume. Re-suspend and connect a new subscriber while the count is transiently zero.
            var suspend2 = cache.SuspendNotifications();
            using var lateResults = cache.Connect().AsAggregator();

            // Let the resume thread proceed to its now-stale resume signal.
            releaseDelivery.Set();
            await Assert.That(resumeThread.Join(TimeSpan.FromSeconds(30))).IsTrue().Because("resume should complete");

            // The late subscriber connected while re-suspended: it must NOT have activated,
            // because notifications ARE suspended (suspend2 is active). The stale resume signal
            // must be suppressed by re-checking the suspend count.
            await Assert.That(lateResults.Messages.Count).IsEqualTo(0).Because("a connection made during re-suspension must not activate on a stale resume signal");
            await Assert.That(lateResults.Data.Count).IsEqualTo(0).Because("no data should be delivered while suspended");

            // Releasing the real suspension delivers the data normally.
            suspend2.Dispose();
            await Assert.That(lateResults.Data.Count).IsEqualTo(dataSet.Count).Because("all data should arrive once truly resumed");
            await Assert.That(lateResults.Messages.Count).IsEqualTo(1).Because("a single changeset on the real resume");
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

        [Test]
        public async Task SuspensionsAreThreadSafe()
        {
            // Arrange
            using var source = new SourceCache<int, int>(static x => x);
            var results = source.Connect().AsAggregator();
            var countChangeHistory = new List<int>();
            using var countChangeSubscription = source.CountChanged.Do(countChangeHistory.Add).Subscribe();

            // Act
            using var suspend = source.SuspendNotifications();
            var tasks = Enumerable.Range(1, 100).Select(x => Task.Run(() => source.AddOrUpdate(x))).ToArray();
            await Task.WhenAll(tasks);

            await Task.Run(suspend.Dispose);

            // Assert
            await Assert.That(results.Data.Count).IsEqualTo(100).Because("Should receive data after resume");
            await Assert.That(results.Messages.Count).IsEqualTo(1).Because("Should receive single changeset on resume");
            await Assert.That(results.Messages[0].Adds).IsEqualTo(100).Because("Should have 100 adds");
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
    }
}
