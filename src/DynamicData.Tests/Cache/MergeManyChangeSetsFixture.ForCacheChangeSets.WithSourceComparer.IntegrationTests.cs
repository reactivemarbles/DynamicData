namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForCacheChangeSets
    {
        public static partial class WithSourceComparer
        {
            public sealed class IntegrationTests
                : IntegrationTestFixtureBase
            {
                [Theory]
                [InlineData(5, 7)]
                [InlineData(10, 50)]
                public async Task MultiThreadedStressTest(int marketCount, int priceCount)
                {
                    const int MaxItemId = 50;
                    var MaxAddTime = TimeSpan.FromSeconds(0.250);
                    var MaxRemoveTime = TimeSpan.FromSeconds(0.100);

                    var randomizer = new Randomizer(0x10012022);
                    var marketFaker = Fakers.Market.RuleFor(m => m.Rating, faker => faker.Random.Double(0, 5)).WithSeed(randomizer);
                    
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var merged = marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices, Market.RatingCompare, resortOnSourceRefresh: true).Publish();
                    var adding = true;
                    var cacheCompleted = merged.LastOrDefaultAsync().ToTask();
                    using var priceResults = merged.AsAggregator();
                    using var connect = merged.Connect();

                    // Start asynchrononously modifying the parent list and the child lists
                    using var addingSub = AddRemoveStress(marketCount, priceCount, Environment.ProcessorCount, TaskPoolScheduler.Default)
                        .Finally(() => adding = false)
                        .Subscribe();

                    // Subscribe / unsubscribe over and over while the collections are being modified
                    do
                    {
                        // Ensure items are being added asynchronously before subscribing to changes
                        await Task.Yield();

                        {
                            // Subscribe
                            var mergedSub = merged.Subscribe();

                            // Let other threads run
                            await Task.Yield();

                            // Unsubscribe
                            mergedSub.Dispose();
                        }
                    }
                    while (adding);

                    // Wait for the source cache to finish delivering all notifications.
                    await cacheCompleted;

                    // Verify the results
                    CheckResultContents(marketCache, marketCacheResults, priceResults, Market.RatingCompare);


                    TimeSpan? GetRemoveTime() => randomizer.Bool() ? randomizer.TimeSpan(MaxRemoveTime) : null;

                    IObservable<Unit> AddRemoveStress(int marketCount, int priceCount, int parallel, IScheduler scheduler) =>
                        Observable.Create<Unit>(observer => new CompositeDisposable
                        {
                            AddRemoveMarkets(marketCount, parallel, scheduler)
                                .Subscribe(
                                    onNext: static _ => { },
                                    onError: observer.OnError),
                            marketCache.Connect()
                                .MergeMany(market => AddRemovePrices((Market)market, priceCount, parallel, scheduler))
                                .Subscribe(
                                    onNext: static _ => { },
                                    onError: observer.OnError,
                                    onCompleted: observer.OnCompleted)
                        });

                    IObservable<IMarket> AddRemoveMarkets(int ownerCount, int parallel, IScheduler scheduler) =>
                        marketFaker.IntervalGenerate(MaxAddTime, scheduler)
                            .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(marketCache, _ => GetRemoveTime(), scheduler))
                            .Finally(marketCache.Dispose);

                    IObservable<MarketPrice> AddRemovePrices(Market market, int priceCount, int parallel, IScheduler scheduler) =>
                        randomizer.Interval(MaxAddTime, scheduler).Select(_ => market.CreatePrice(randomizer.Number(MaxItemId), GetRandomPrice(randomizer)))
                            .Parallelize(priceCount, parallel, obs => obs.StressAddRemove(market.PricesCache, _ => GetRemoveTime(), scheduler))
                            .Finally(market.PricesCache.Dispose);
                }

                private static void CheckResultContents(
                    SourceCache<IMarket, Guid> marketCache,
                    ChangeSetAggregator<IMarket, Guid> marketResults,
                    ChangeSetAggregator<MarketPrice, int> priceResults,
                    IComparer<IMarket> comparer)
                {
                    var expectedMarkets = marketCache.Items.ToList();

                    // These should be subsets of each other
                    expectedMarkets.Should().BeSubsetOf(marketResults.Data.Items);
                    marketResults.Data.Items.Count.Should().Be(expectedMarkets.Count);

                    // Pair up all the Markets/Prices, Group them by ItemId, and sort each Group by the Market comparer
                    // Then pull out the first value from each group, which should be the price from the best market for each ItemId
                    var expectedPrices = expectedMarkets.Select(m => (Market)m).SelectMany(m => m.PricesCache.Items.Select(mp => (Market: m, MarketPrice: mp)))
                        .GroupBy(tuple => tuple.MarketPrice.ItemId)
                        .Select(group => group.OrderBy(tuple => tuple.Market, comparer).Select(tuple => tuple.MarketPrice).First())
                        .ToList();

                    // These should be subsets of each other
                    expectedPrices.Should().BeSubsetOf(priceResults.Data.Items);
                    priceResults.Data.Items.Count.Should().Be(expectedPrices.Count);
                }
            }
        }
    }
}
