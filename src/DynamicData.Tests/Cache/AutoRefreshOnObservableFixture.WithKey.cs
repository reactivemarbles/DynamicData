namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshOnObservableFixture
{
    public class WithKey
        : Base
    {
        [Fact]
        public void ReevaluatorIsNull_ThrowsException()
            => FluentActions.Invoking(() => ObservableCacheEx.AutoRefreshOnObservable(
                    source:         Observable.Never<IChangeSet<Item, int>>(),
                    reevaluator:    (null as Func<Item, int, IObservable<Unit>>)!))
                .Should()
                .Throw<ArgumentNullException>();
            
        protected override IObservable<IChangeSet<Item, int>> BuildUut<TAny>(
                IObservable<IChangeSet<Item, int>>  source,
                Func<Item, IObservable<TAny>>       reevaluator,
                TimeSpan?                           changeSetBuffer = null,
                IScheduler?                         scheduler       = null)
            => source.AutoRefreshOnObservable(
                reevaluator:        (item, _) => reevaluator.Invoke(item),
                changeSetBuffer:    changeSetBuffer,
                scheduler:          scheduler);
    }
}
