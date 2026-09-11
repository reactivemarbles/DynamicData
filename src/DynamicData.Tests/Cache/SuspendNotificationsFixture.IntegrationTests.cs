using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
            results.Data.Count.Should().Be(100, "Should receive data after resume");
            results.Messages.Count.Should().Be(1, "Should receive single changeset on resume");
            results.Messages[0].Adds.Should().Be(100, "Should have 100 adds");
        }
    }
}

