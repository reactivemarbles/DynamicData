using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class ToObservableChangeSetFixture
{
    public static partial class Items
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Theory(Timeout = 60_000)]
            [InlineData(SchedulerType.Default)]
            [InlineData(SchedulerType.NewThread)]
            [InlineData(SchedulerType.TaskPool)]
            [InlineData(SchedulerType.ThreadPool)]
            public async Task MultipleSubscriptionsRunInParallel_SchedulerUsageIsThreadSafe(SchedulerType schedulerType)
            {
                IScheduler scheduler = schedulerType switch
                {
                    SchedulerType.Default       => DefaultScheduler.Instance,
                    SchedulerType.NewThread     => new NewThreadScheduler(),
                    SchedulerType.TaskPool      => TaskPoolScheduler.Default,
                    SchedulerType.ThreadPool    => ThreadPoolScheduler.Instance,
                    _                           => throw new ArgumentOutOfRangeException(nameof(SchedulerType))
                };

                using var subscription1 = Observable.Interval(
                        period:     TimeSpan.FromMilliseconds(5),
                        scheduler:  scheduler)
                    .Take(IntegrationTestItemCount)
                    .Select(id => new Item()
                    {
                        Id          = (int)(id % 100),
                        Lifetime    = TimeSpan.FromMilliseconds(50)
                    })
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results1);
        
                using var subscription2 = Observable.Interval(
                        period:     TimeSpan.FromMilliseconds(5),
                        scheduler:  scheduler)
                    .Take(IntegrationTestItemCount)
                    .Select(id => new Item()
                    {
                        Id          = (int)(id % 100) + 100,
                        Lifetime    = TimeSpan.FromMilliseconds(50)
                    })
                    .ToObservableChangeSet(
                        keySelector:    Item.SelectId,
                        expireAfter:    Item.SelectLifetime,
                        scheduler:      scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results2);

                await Task.WhenAll(
                    results1.WhenFinalized,
                    results2.WhenFinalized);

                results1.Error.Should().BeNull();
                results1.HasCompleted.Should().BeTrue("all changes should have been processed successfully");
                results1.RecordedItemsByKey.Should().BeEmpty("all items should have expired");

                results2.Error.Should().BeNull();
                results2.HasCompleted.Should().BeTrue("all changes should have been processed successfully");
                results2.RecordedItemsByKey.Should().BeEmpty("all items should have expired");
            }
        }
    }
}
