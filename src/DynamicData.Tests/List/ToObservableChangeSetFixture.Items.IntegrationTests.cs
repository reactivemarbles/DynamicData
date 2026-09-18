namespace DynamicData.Tests.List;

public static partial class ToObservableChangeSetFixture
{
    public static partial class Items
    {
        public class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Test]
            [Timeout(60_000)]
            [Arguments(SchedulerType.Default)]
            [Arguments(SchedulerType.NewThread)]
            [Arguments(SchedulerType.TaskPool)]
            [Arguments(SchedulerType.ThreadPool)]
            public async Task MultipleSubscriptionsRunInParallel_SchedulerUsageIsThreadSafe(SchedulerType schedulerType, CancellationToken cancellationToken)
            {
                IScheduler scheduler = schedulerType switch
                {
                    SchedulerType.Default => Scheduler.Default,
                    SchedulerType.NewThread => new NewThreadScheduler(),
                    SchedulerType.TaskPool => TaskPoolScheduler.Default,
                    SchedulerType.ThreadPool => ThreadPoolScheduler.Instance,
                    _ => throw new ArgumentOutOfRangeException(nameof(SchedulerType))
                };

                using var subscription1 = Observable.Interval(
                        period: TimeSpan.FromMilliseconds(5),
                        scheduler: scheduler)
                    .Take(IntegrationTestItemCount)
                    .Select(id => new Item()
                    {
                        Id = (int)(id % 100),
                        Lifetime = TimeSpan.FromMilliseconds(50)
                    })
                    .ToObservableChangeSet(
                        expireAfter: Item.SelectLifetime,
                        scheduler: scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results1);

                using var subscription2 = Observable.Interval(
                        period: TimeSpan.FromMilliseconds(5),
                        scheduler: scheduler)
                    .Take(IntegrationTestItemCount)
                    .Select(id => new Item()
                    {
                        Id = (int)(id % 100) + 100,
                        Lifetime = TimeSpan.FromMilliseconds(50)
                    })
                    .ToObservableChangeSet(
                        expireAfter: Item.SelectLifetime,
                        scheduler: scheduler)
                    .ValidateSynchronization()
                    .ValidateChangeSets()
                    .RecordListItems(out var results2);

                await Task.WhenAll(
                    results1.WhenFinalized,
                    results2.WhenFinalized).WaitAsync(cancellationToken);

                await Assert.That(results1.Error).IsNull();
                await Assert.That(results1.HasCompleted).IsTrue().Because("all changes should have been processed successfully");
                await Assert.That(results1.RecordedItems).IsEmpty().Because("all items should have expired");

                await Assert.That(results2.Error).IsNull();
                await Assert.That(results2.HasCompleted).IsTrue().Because("all changes should have been processed successfully");
                await Assert.That(results2.RecordedItems).IsEmpty().Because("all items should have expired");
            }
        }
    }
}
