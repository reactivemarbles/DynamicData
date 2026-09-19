namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    [InheritsTests]
    public class WithoutPropertyAccessor
        : Base
    {
        [Test]
        public async Task PropertyChangedNotificationDoesNotSpecifyPropertyName_ItemRefreshes()
        {
            // Setup
            using var source = new TestSourceCache<Item, int>(Item.SelectId);

            var item1 = new Item() { Id = 1 };
            var item2 = new Item() { Id = 2 };
            var item3 = new Item() { Id = 3 };

            source.AddOrUpdate(new[] { item1, item2, item3 });

            // UUT Initialization
            using var subscription = BuildUut(source.Connect())
                .ValidateSynchronization()
                .ValidateChangeSets(Item.SelectId)
                .RecordCacheItems(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Count).IsEqualTo(1).Because("the initial changeset should propagate");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("3 items were added to the source");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

            // UUT Action
            item2.RaiseAllPropertiesChanged();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedChangeSets.Skip(1).Count()).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Count).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().Refreshes).IsEqualTo(1).Because("1 item published a property change notification");
            await Assert.That(results.RecordedChangeSets.Skip(1).First().First().Current).IsEqualTo(item2).Because("item #2 published a property change notification");
            await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(source.Items).Because("no source operations were performed");
            await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");
        }

        protected override IObservable<IChangeSet<Item, int>> BuildUut(
                IObservable<IChangeSet<Item, int>> source,
                TimeSpan? changeSetBuffer = null,
                TimeSpan? propertyChangeThrottle = null,
                IScheduler? scheduler = null)
            => source.AutoRefresh(
                changeSetBuffer: changeSetBuffer,
                propertyChangeThrottle: propertyChangeThrottle,
                scheduler: scheduler);
    }
}
