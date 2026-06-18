using FluentAssertions;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshOnObservableFixture
{
    public class WithoutKey
        : Base
    {
        [Fact(Skip = "Existing defect: reevaluator is not null checked, throws NRW on first notification, instead")]
        public void ReevaluatorIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.AutoRefreshOnObservable(
                    source:         Observable.Never<IChangeSet<Item, int>>(),
                    reevaluator:    (null as Func<Item, IObservable<Unit>>)!))
                .Should()
                .Throw<ArgumentNullException>();
            
        protected override IObservable<IChangeSet<Item, int>> BuildUut<TAny>(
                IObservable<IChangeSet<Item, int>>  source,
                Func<Item, IObservable<TAny>>       reevaluator,
                TimeSpan?                           changeSetBuffer = null,
                IScheduler?                         scheduler       = null)
            => source.AutoRefreshOnObservable(
                reevaluator:        reevaluator,
                changeSetBuffer:    changeSetBuffer,
                scheduler:          scheduler);
    }
}
