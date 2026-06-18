using System.Linq.Expressions;

using FluentAssertions;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    public class WithPropertyAccessor
        : Base
    {
        [Fact(Skip = "Existing defect: propertyAccessor is not null checked, throws NRE on first notification, instead")]
        public void PropertyAccessorIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.AutoRefresh(
                    source:             Observable.Never<IChangeSet<Item, int>>(),
                    propertyAccessor:   (null as Expression<Func<Item, int>>)!))
                .Should()
                .Throw<ArgumentNullException>();
                
        [Fact]
        public void PropertyChangedNotificationDoesNotMatchPropertyAccessor_IgnoresNotification()
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
            ++item2.OtherValue;

            results.Error.Should().BeNull();
            results.RecordedChangeSets.Skip(1).Should().BeEmpty("the property change notification should have been ignored");
            results.HasCompleted.Should().BeFalse("the source has not completed");
        }
            
        protected override IObservable<IChangeSet<Item, int>> BuildUut(
                IObservable<IChangeSet<Item, int>>  source,
                TimeSpan?                           changeSetBuffer         = null,
                TimeSpan?                           propertyChangeThrottle  = null,
                IScheduler?                         scheduler               = null)
            => source.AutoRefresh(
                propertyAccessor:       static item => item.Value,
                changeSetBuffer:        changeSetBuffer,
                propertyChangeThrottle: propertyChangeThrottle,
                scheduler:              scheduler);
    }
}
