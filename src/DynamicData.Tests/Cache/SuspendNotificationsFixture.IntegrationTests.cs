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

        [Fact]
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
            results.Messages.Count.Should().Be(0, "no messages during suspension");

            // Resume on background thread — delivery blocks on slow subscriber
            var resumeTask = Task.Run(() => suspend1.Dispose());
            (await delivering.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("delivery should have started");

            // Re-suspend and write second batch while delivery is blocked
            var suspend2 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet2);

            // dataSet2 must not appear in any message received so far
            foreach (var msg in results.Messages)
            {
                foreach (var change in msg)
                {
                    change.Key.Should().BeInRange(0, 99,
                        "deferred subscriber should only have first-batch keys before second resume");
                }
            }

            // Unblock delivery
            proceedWithResuspend.Release();
            await resumeTask;

            // Only dataSet1 should have been delivered — dataSet2 is held by second suspension
            results.Summary.Overall.Adds.Should().Be(dataSet1.Count,
                $"exactly {dataSet1.Count} adds before second resume — dataSet2 must be held by suspension");
            results.Messages.Should().HaveCount(1, "exactly one message (snapshot of dataSet1)");
            results.Messages[0].Adds.Should().Be(dataSet1.Count);
            results.Messages[0].Select(c => c.Key).Should().Equal(dataSet1,
                "snapshot should contain exactly first-batch keys in order");

            // Resume second suspension — dataSet2 arrives now
            suspend2.Dispose();

            results.Summary.Overall.Adds.Should().Be(allData.Count, $"exactly {allData.Count} adds total");
            results.Summary.Overall.Removes.Should().Be(0, "no removes");
            results.Messages.Should().HaveCount(2, "two messages: snapshot + second batch");
            results.Messages[1].Adds.Should().Be(dataSet2.Count);
            results.Messages[1].Select(c => c.Key).Should().Equal(dataSet2,
                "second message should contain exactly second-batch keys in order");
            results.Data.Count.Should().Be(allData.Count);
            results.Data.Keys.OrderBy(k => k).Should().Equal(allData);
            results.Error.Should().BeNull();
            results.IsCompleted.Should().BeFalse();
        }
    }
}
