using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public static partial class SuspendNotificationsFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Fact]
        public void ResumeDeliversPendingChangesWithoutHoldingTheLock()
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
            deliveryStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("delivery of pending changes should have started");

            // While delivery is blocked, a concurrent lock-requiring operation must complete
            // promptly. It cannot if the delivery is happening under the cache lock. The
            // distinguishing gap is large (milliseconds if the lock is free, ~30s if held),
            // so a generous threshold stays reliable under cold-start JIT and heavy load.
            var concurrentThread = new Thread(() =>
            {
                cache.SuspendNotifications().Dispose();
                concurrentOpDone.Set();
            }) { IsBackground = true };
            concurrentThread.Start();

            var completedWhileBlocked = concurrentOpDone.Wait(TimeSpan.FromSeconds(10));

            releaseDelivery.Set();
            resumeThread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("resume should complete");
            concurrentThread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("concurrent operation should complete");

            completedWhileBlocked.Should().BeTrue("a concurrent operation must not block while pending changes are delivered; the lock must not be held during delivery");
            cache.Count.Should().Be(10, "all items should be present after resume");
        }

        [Fact]
        public void StaleResumeSignalIsSuppressedByConcurrentReSuspend()
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
            deliveryStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("delivery should have started");

            // The resume thread is parked after decrementing the count but before signalling
            // resume. Re-suspend and connect a new subscriber while the count is transiently zero.
            var suspend2 = cache.SuspendNotifications();
            using var lateResults = cache.Connect().AsAggregator();

            // Let the resume thread proceed to its now-stale resume signal.
            releaseDelivery.Set();
            resumeThread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("resume should complete");

            // The late subscriber connected while re-suspended: it must NOT have activated,
            // because notifications ARE suspended (suspend2 is active). The stale resume signal
            // must be suppressed by re-checking the suspend count.
            lateResults.Messages.Count.Should().Be(0, "a connection made during re-suspension must not activate on a stale resume signal");
            lateResults.Data.Count.Should().Be(0, "no data should be delivered while suspended");

            // Releasing the real suspension delivers the data normally.
            suspend2.Dispose();
            lateResults.Data.Count.Should().Be(dataSet.Count, "all data should arrive once truly resumed");
            lateResults.Messages.Count.Should().Be(1, "a single changeset on the real resume");
        }

        [Fact]
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

                results.Summary.Overall.Adds.Should().Be(allData.Count, $"iteration {iter}: exactly {allData.Count} adds");
                results.Summary.Overall.Removes.Should().Be(0, $"iteration {iter}: no removes");
                results.Summary.Overall.Updates.Should().Be(0, $"iteration {iter}: no updates because keys don't overlap");
                results.Data.Count.Should().Be(allData.Count, $"iteration {iter}: {allData.Count} items in final state");
                results.Data.Keys.OrderBy(k => k).Should().Equal(allData, $"iteration {iter}: all keys present in order");
                results.Error.Should().BeNull($"iteration {iter}: no errors");
                results.IsCompleted.Should().BeFalse($"iteration {iter}: not completed");
            }
        }
    }
}
