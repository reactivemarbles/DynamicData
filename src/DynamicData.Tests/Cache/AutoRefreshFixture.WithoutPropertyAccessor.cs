using System;
using System.Linq;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    public class WithoutPropertyAccessor
        : Base
    {
        [Fact]
        public void PropertyChangedNotificationDoesNotSpecifyPropertyName_ItemRefreshes()
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

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Count.Should().Be(1, "the initial changeset should propagate");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "3 items were added to the source");
            results.HasCompleted.Should().BeFalse("the source has not completed");

            // UUT Action
            item2.RaiseAllPropertiesChanged();

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Count().Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().Count.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().Refreshes.Should().Be(1, "1 item published a property change notification");
            results.RecordedChangeSets.Skip(1).First().First().Current.Should().Be(item2, "item #2 published a property change notification");
            results.RecordedItemsByKey.Values.Should().BeEquivalentTo(source.Items, "no source operations were performed");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
            
        protected override IObservable<IChangeSet<Item, int>> BuildUut(
                IObservable<IChangeSet<Item, int>>  source,
                TimeSpan?                           changeSetBuffer         = null,
                TimeSpan?                           propertyChangeThrottle  = null,
                IScheduler?                         scheduler               = null)
            => source.AutoRefresh(
                changeSetBuffer:        changeSetBuffer,
                propertyChangeThrottle: propertyChangeThrottle,
                scheduler:              scheduler);
    }
}
