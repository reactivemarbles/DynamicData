using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class SuspendNotificationsFixture
{
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        [Fact(Timeout = 30_000)]
        public async Task StaleResumeSignalIsSuppressedByConcurrentReSuspend()
        {
            // Deterministic reproduction of the suspend/resume state-divergence race (#1131).
            // Resume decrements the suspend count in one step and emits its resume signal in a
            // later step. If a re-suspend slips in between, the resume signal must NOT fire:
            // otherwise the suspended-notification subject would say "resumed" while the count
            // says "suspended", and a connection made during the re-suspension would wrongly
            // activate and receive data while notifications are suspended.

            // The interleaving is forced deterministically: an active subscriber blocks the
            // resume thread inside delivery, after the count has been decremented to zero but
            // before the resume signal, giving the main thread a window to re-suspend and connect.
            using var cache = new SourceCache<int, int>(static x => x);
            var dataSet = Enumerable.Range(0, 50).ToList();

            // Setup a subscription that will partially block suspension disposal.
            var whenDeliveryStarted         = new TaskCompletionSource();
            var whenDeliveryShouldResume    = new ManualResetEventSlim();
            var notificationCount = 0;
            using var firstSubscription = cache
                .Connect()
                .Subscribe(_ =>
                {
                    ++notificationCount;
                    if (notificationCount is 1)
                    {
                        whenDeliveryStarted.SetResult();
                        whenDeliveryShouldResume.Wait();
                    }
                });   

            // Setup a suspended notification, to be delivered later.
            var firstSuspension = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet);

            // Setup a background thread to dispose the suspension, and deliver the suspended notification.
            var whenFirstSuspensionDisposed = Task.Run(firstSuspension.Dispose);
            
            // Setup a background thread to attempt to take out another suspension, while the first one is still in-progress of disposing.
            // This should block until the first suspension is fully disposed.
            var whenSecondSuspensionActivating  = new TaskCompletionSource();
            var whenSecondSuspensionActivated   = new TaskCompletionSource();
            var whenSecondSuspensionDisposed = Task.Run(async () =>
            {
                await whenDeliveryStarted.Task;
                whenSecondSuspensionActivating.SetResult();
                using var secondSuspension = cache.SuspendNotifications();
                whenSecondSuspensionActivated.SetResult();
            });

            // Setup a background thread to attempt to take out another subscription, while the first suspension is still in-progress of disposing.
            // This should also block, until the first suspension is fully disposed.
            var whenSecondSubscriberInitializing    = new TaskCompletionSource();
            var whenSecondSubscriberInitialized     = new TaskCompletionSource();
            var whenSecondSubscriberDisposed = Task.Run(async () =>
            {
                await whenDeliveryStarted.Task;
                whenSecondSubscriberInitializing.SetResult();
                using var secondSubscription = cache
                    .Connect()
                    .RecordCacheItems(out var results);
                whenSecondSubscriberInitialized.SetResult();
                
                await whenSecondSuspensionDisposed;
                
                results.Error.Should().BeNull("no errors should have occurred");
                results.RecordedChangeSets.Should().ContainSingle("the suspended notification should have been delivered");
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(dataSet, "the notification should have published all added items");
            });

            // Wait for the first suspension to get parked, part-way through.
            await whenDeliveryStarted.Task;
            
            // Wait for the background actions to catch up.
            await Task.WhenAll(
                whenSecondSuspensionActivating.Task,
                whenSecondSubscriberInitializing.Task);

            // Make sure that the background actions are blocked.
            await Task.Delay(TimeSpan.FromSeconds(1));
            whenSecondSuspensionActivated.Task.IsCompleted.Should().BeFalse("suspending notifications while another suspension is in the process of disposing should block");            
            whenSecondSubscriberInitialized.Task.IsCompleted.Should().BeFalse("subscribing to the cache while a suspension is in the process of disposing should block");            

            // Unblock the suspension disposal, and let everything wrap up.
            whenDeliveryShouldResume.Set();
            await Task.WhenAll(
                whenFirstSuspensionDisposed,
                whenSecondSubscriberDisposed,
                whenSecondSuspensionActivated.Task,
                whenSecondSuspensionDisposed,
                whenSecondSubscriberInitialized.Task,
                whenSecondSubscriberDisposed);
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
