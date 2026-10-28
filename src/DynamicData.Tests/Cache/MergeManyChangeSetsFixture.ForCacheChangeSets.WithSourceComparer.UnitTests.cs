namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForCacheChangeSets
    {
        public static partial class WithSourceComparer
        {
            public class UnitTests
            {
                [Fact]
                public void AbleToInvokeFactory()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    // having
                    var invoked = false;
                    IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m)
                    {
                        invoked = true;
                        return m.LatestPrices;
                    }
                    using var sub = marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

                    // when
                    marketCache.AddOrUpdate(new Market(0));

                    // then
                    marketCacheResults.Data.Count.Should().Be(1);
                    invoked.Should().BeTrue();
                }

                [Fact]
                public void AbleToInvokeFactoryWithKey()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    // having
                    var invoked = false;
                    IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m, Guid g)
                    {
                        invoked = true;
                        return m.LatestPrices;
                    }
                    using var sub = marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

                    // when
                    marketCache.AddOrUpdate(new Market(0));

                    // then
                    marketCacheResults.Data.Count.Should().Be(1);
                    invoked.Should().BeTrue();
                }

                [Fact]
                public void AllExistingSubItemsPresentInResult()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, () => GetRandomPrice(randomizer)));

                    // when
                    marketCache.AddOrUpdate(markets);

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
                    results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
                    results.Messages.Count.Should().Be(1);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AllNewSubItemsPresentInResult()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    marketCache.AddOrUpdate(markets);

                    // when
                    markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, () => GetRandomPrice(randomizer)));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
                    results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
                    results.Messages.Count.Should().Be(MarketCount);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AllRefreshedSubItemsAreRefreshed()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, () => GetRandomPrice(randomizer)));
                    marketCache.AddOrUpdate(markets);

                    // when
                    markets.ForEach(m => m.RefreshAllPrices(() => GetRandomPrice(randomizer)));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
                    results.Messages.Count.Should().Be(MarketCount + 1);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(MarketCount * PricesPerMarket);
                }

                [Fact]
                public void AnyDuplicateKeyValuesShouldBeHidden()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);

                    // when
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // when
                    markets[1].RemoveAllPrices();

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // when
                    marketCache.Remove(markets[0]);

                    // then
                    marketCacheResults.Data.Count.Should().Be(1);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Data.Items.Zip(markets[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Messages.Count.Should().Be(2);
                    results.Messages[1].Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // when
                    markets[1].RefreshAllPrices(() => GetRandomPrice(randomizer));

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AnyRemovedSubItemIsRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    marketCache.AddOrUpdate(markets);
                    markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, () => GetRandomPrice(randomizer)));

                    // when
                    markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
                    results.Messages.Count.Should().Be(MarketCount * 2);
                    results.Messages[0].Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void AnySourceItemRemovedRemovesAllSourceValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    marketCache.AddOrUpdate(markets);
                    markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, () => GetRandomPrice(randomizer)));

                    // when
                    marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount - RemoveCount);
                    results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void BestChoiceFromDuplicatesSelectedWhenChangeSetCreated()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var marketOriginal = new Market(0);
                    var marketBetter = new Market(1);
                    marketBetter.Rating = 1.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketBetter.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketBetter);

                    // when
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var resultsLow = ChangeSetByLowRating(marketCache, false).AsAggregator();

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    var market = new Market(0);
                    market.SetPrices(0, PricesPerMarket * 2, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(market);
                    var updatedMarket = new Market(market);
                    updatedMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, () => GetRandomPrice(randomizer));

                    // when
                    marketCache.AddOrUpdate(updatedMarket);

                    // then
                    marketCacheResults.Data.Count.Should().Be(1);
                    results.Data.Count.Should().Be(PricesPerMarket * 2);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Zip(updatedMarket.PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                }

                [Fact]
                public void ChangingSourceByUpdateRemovesPreviousAndEmitsBetterValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    var market = new Market(0);
                    var marketWorse = new Market(1);
                    SetRating(marketCache, marketWorse, -1);
                    market.SetPrices(0, PricesPerMarket * 2, () => GetRandomPrice(randomizer));
                    marketWorse.SetPrices(0, PricesPerMarket * 2, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(market);
                    marketCache.AddOrUpdate(marketWorse);

                    var updatedMarket = new Market(market);
                    updatedMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, () => GetRandomPrice(randomizer));

                    // when
                    marketCache.AddOrUpdate(updatedMarket);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket * 3);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Take(PricesPerMarket).Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketWorse.Id));
                    results.Data.Items.Skip(PricesPerMarket).Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(updatedMarket.Id));
                }

                [Fact]
                public void ChildComparerOnlyRefreshesVisibleValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var lowRatingLowPriceResults = ChangeSetByLowRatingThenLowPrice(marketCache, false).AsAggregator();
                    using var lowRatingHighPriceResults = ChangeSetByLowRatingThenHighPrice(marketCache, false).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketLowest = new Market(2);

                    marketLowest.Rating = marketLow.Rating = -1;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLow.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLowest.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(marketLowest);

                    // when
                    marketLowest.RefreshAllPrices(LowestPrice - 1);

                    // then
                    marketCacheResults.Data.Count.Should().Be(3);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                    lowRatingLowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowRatingLowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowRatingLowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowRatingLowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    lowRatingLowPriceResults.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
                    lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));
                    lowRatingHighPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowRatingHighPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowRatingHighPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowRatingHighPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowRatingHighPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                }

                [Fact]
                public void ChildComparerOnlyUpdatesVisibleValuesOnUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var lowRatingLowPriceResults = ChangeSetByLowRatingThenLowPrice(marketCache, false).AsAggregator();
                    using var lowRatingHighPriceResults = ChangeSetByLowRatingThenHighPrice(marketCache, false).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketLowest = new Market(2);

                    marketLowest.Rating = marketLow.Rating = -1;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketLowest.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(marketLowest);

                    // when
                    marketLowest.UpdateAllPrices(LowestPrice - 1);

                    // then
                    marketCacheResults.Data.Count.Should().Be(3);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                    lowRatingLowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowRatingLowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowRatingLowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowRatingLowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    lowRatingLowPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));
                    lowRatingHighPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowRatingHighPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowRatingHighPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowRatingHighPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    lowRatingHighPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                }

                [Fact]
                public void ChildComparerUpdatesToCorrectValueOnUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var resultsLow = ChangeSetByLowRating(marketCache, false).AsAggregator();
                    using var resultsLowPrice = ChangeSetByRatingThenLowPrice(marketCache, false).AsAggregator();
                    using var resultsHighPrice = ChangeSetByRatingThenHighPrice(marketCache, false).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketHighest = new Market(1);
                    var marketLowest = new Market(2);
                    marketLowest.Rating = marketHighest.Rating = 1.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketHighest.SetPrices(0, PricesPerMarket, HighestPrice);
                    marketLowest.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketHighest);
                    marketCache.AddOrUpdate(marketLowest);

                    // when
                    marketLowest.UpdateAllPrices(LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(3);
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));

                    resultsLowPrice.Data.Count.Should().Be(PricesPerMarket);
                    resultsLowPrice.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLowPrice.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    resultsLowPrice.Summary.Overall.Removes.Should().Be(0);
                    resultsLowPrice.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLowPrice.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));

                    resultsHighPrice.Data.Count.Should().Be(PricesPerMarket);
                    resultsHighPrice.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsHighPrice.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    resultsHighPrice.Summary.Overall.Removes.Should().Be(0);
                    resultsHighPrice.Summary.Overall.Refreshes.Should().Be(0);
                    resultsHighPrice.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketHighest.Id));
                }

                [Fact]
                public void EqualityComparerAndChildComparerRefreshesBecomeUpdates()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var resultsLow = ChangeSetByLowRating(marketCache).AsAggregator();
                    using var resultsRecent = ChangeSetByRatingThenRecent(marketCache).AsAggregator();
                    using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp(marketCache).AsAggregator();
                    var marketLow = new Market(0);
                    var market = new Market(1);
                    marketLow.Rating = -1;
                    marketLow.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    market.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(market);
                    market.SetPrices(0, PricesPerMarket, LowestPrice);
                    // Update again, but only the timestamp will change, so resultsRecent will ignore
                    market.SetPrices(0, PricesPerMarket, LowestPrice);

                    // when
                    // resultsRecent won't see the refresh because it ignored the update
                    // resultsTimeStamp will see the refreshes because it didn't
                    market.RefreshAllPrices(() => GetRandomPrice(randomizer));

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Messages.Count.Should().Be(1);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsRecent.Messages.Count.Should().Be(4);
                    resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsRecent.Summary.Overall.Removes.Should().Be(0);
                    resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    resultsRecent.Summary.Overall.Refreshes.Should().Be(0);
                    resultsTimeStamp.Messages.Count.Should().Be(5);
                    resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
                    resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
                }

                [Fact]
                public void EqualityComparerAndChildComparerWorkTogetherForRefreshes()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var resultsLow = ChangeSetByLowRating(marketCache).AsAggregator();
                    using var resultsRecent = ChangeSetByRatingThenRecent(marketCache).AsAggregator();
                    using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp(marketCache).AsAggregator();
                    var marketLow = new Market(0);
                    var market = new Market(1);
                    marketLow.Rating = -1;
                    marketLow.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    market.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(market);
                    market.SetPrices(0, PricesPerMarket, LowestPrice);
                    // Update again, but only the timestamp will change, so resultsRecent will ignore
                    market.SetPrices(0, PricesPerMarket, LowestPrice);

                    // when
                    // resultsRecent won't see the refresh because it ignored the update
                    // resultsTimeStamp will see the refreshes because it didn't
                    market.RefreshAllPrices(LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Messages.Count.Should().Be(1);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsRecent.Messages.Count.Should().Be(4);
                    resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsRecent.Summary.Overall.Removes.Should().Be(0);
                    resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    resultsRecent.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
                    resultsTimeStamp.Messages.Count.Should().Be(5);
                    resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
                    resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
                }

                [Fact]
                public void EqualityComparerAndChildComparerWorkTogetherForUpdates()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var resultsLow = ChangeSetByLowRating(marketCache).AsAggregator();
                    using var resultsRecent = ChangeSetByRatingThenRecent(marketCache).AsAggregator();
                    using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp(marketCache).AsAggregator();
                    var marketLow = new Market(0);
                    var market = new Market(1);
                    marketLow.Rating = -1;
                    marketLow.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    market.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(market);
                    market.SetPrices(0, PricesPerMarket, LowestPrice);

                    // when
                    market.UpdateAllPrices(LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Messages.Count.Should().Be(1);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsRecent.Messages.Count.Should().Be(3);
                    resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsRecent.Summary.Overall.Removes.Should().Be(0);
                    resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    resultsRecent.Summary.Overall.Refreshes.Should().Be(0);
                    resultsTimeStamp.Messages.Count.Should().Be(4);
                    resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
                    resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
                    resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void EqualityComparerHidesUpdatesWithoutChanges()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    // having
                    var market = new Market(0);
                    using var results = CreateChangeSet(marketCache, "Equality Compare", Market.RatingCompare, equalityComparer: MarketPrice.EqualityComparer, resortOnRefresh: true).AsAggregator();
                    market.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketCache.AddOrUpdate(market);

                    // when
                    market.SetPrices(0, PricesPerMarket, LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(1);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Messages.Count.Should().Be(1);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void EveryItemVisibleWhenSequenceCompletes()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(() => GetRandomPrice(randomizer), n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

                    // when
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
                    marketCache.Dispose();

                    // then
                    results.Data.Count.Should().Be(PricesPerMarket * MarketCount);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket * MarketCount);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Theory]
                [InlineData(false, false)]
                [InlineData(false, true)]
                [InlineData(true, false)]
                [InlineData(true, true)]
                public void MergedObservableCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(() => GetRandomPrice(randomizer), n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
                    var hasSourceSequenceCompleted = false;
                    var hasMergedSequenceCompleted = false;

                    using var cleanup = marketCache.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
                        .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

                    // when
                    if (completeSource)
                    {
                        marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
                        marketCache.Dispose();
                    }

                    // then
                    hasSourceSequenceCompleted.Should().Be(completeSource);
                    hasMergedSequenceCompleted.Should().Be(completeSource && completeChildren);
                }

                [Fact]
                public void MergedObservableWillFailIfSourceFails()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    marketCache.AddOrUpdate(markets);
                    var receivedError = default(Exception);
                    var expectedError = new Exception("Test exception");
                    var throwObservable = Observable.Throw<IChangeSet<IMarket, Guid>>(expectedError);

                    using var cleanup = marketCache.Connect().Concat(throwObservable)
                        .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, err => receivedError = err);

                    // when
                    marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
                    marketCache.Dispose();

                    // then
                    receivedError.Should().Be(expectedError);
                }

                [Fact]
                public void NullChecks()
                {
                    // having
                    var emptyChangeSetObs = Observable.Empty<IChangeSet<int, int>>();
                    var nullChangeSetObs = (IObservable<IChangeSet<int, int>>)null!;
                    var emptyChildChangeSetObs = Observable.Empty<IChangeSet<string, string>>();
                    var emptySelector = new Func<int, IObservable<IChangeSet<string, string>>>(i => emptyChildChangeSetObs);
                    var emptyKeySelector = new Func<int, int, IObservable<IChangeSet<string, string>>>((i, key) => emptyChildChangeSetObs);
                    var nullSelector = (Func<int, IObservable<IChangeSet<string, string>>>)null!;
                    var nullKeySelector = (Func<int, int, IObservable<IChangeSet<string, string>>>)null!;
                    var nullParentComparer = (IComparer<int>)null!;
                    var emptyParentComparer = new NoOpComparer<int>() as IComparer<int>;
                    var nullChildComparer = (IComparer<string>)null!;
                    var emptyChildComparer = new NoOpComparer<string>() as IComparer<string>;
                    var nullEqualityComparer = (IEqualityComparer<string>)null!;
                    var emptyEqualityComparer = new NoOpEqualityComparer<string>() as IEqualityComparer<string>;

                    // when
                    var actionParentCompare1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, sourceComparer: emptyParentComparer);
                    var actionParentCompareKey1a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: emptyParentComparer);
                    var actionParentCompareKey1b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, sourceComparer: emptyParentComparer);
                    var actionParentCompareKey1c = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: nullParentComparer);
                    var actionParentCompare2 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);
                    var actionParentCompareKey2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);
                    var actionParentCompareKey2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);

                    // then
                    emptyChangeSetObs.Should().NotBeNull();
                    emptyChildChangeSetObs.Should().NotBeNull();
                    emptyChildComparer.Should().NotBeNull();
                    emptyEqualityComparer.Should().NotBeNull();
                    emptyKeySelector.Should().NotBeNull();
                    emptyParentComparer.Should().NotBeNull();
                    emptySelector.Should().NotBeNull();
                    nullChangeSetObs.Should().BeNull();
                    nullChildComparer.Should().BeNull();
                    nullEqualityComparer.Should().BeNull();
                    nullKeySelector.Should().BeNull();
                    nullParentComparer.Should().BeNull();
                    nullSelector.Should().BeNull();

                    actionParentCompare1.Should().Throw<ArgumentNullException>();
                    actionParentCompareKey1a.Should().Throw<ArgumentNullException>();
                    actionParentCompareKey1b.Should().Throw<ArgumentNullException>();
                    actionParentCompareKey1c.Should().Throw<ArgumentNullException>();
                    actionParentCompare2.Should().Throw<ArgumentNullException>();
                    actionParentCompareKey2a.Should().Throw<ArgumentNullException>();
                    actionParentCompareKey2b.Should().Throw<ArgumentNullException>();
                }

                [Fact]
                public void OnlyAddsBetterValuesOnSourceUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var resultsLow = ChangeSetByLowRating(marketCache, false).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketBetter = new Market(1);
                    marketBetter.Rating = 1.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketBetter);

                    // when
                    marketBetter.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void OnlyUpdatesOnDuplicateIfNewItemIsFromBetterParent()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var resultsLow = ChangeSetByLowRating(marketCache, false).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketBetter = new Market(1);
                    marketBetter.Rating = 1.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketBetter.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);

                    // when
                    marketCache.AddOrUpdate(marketBetter);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Updates.Should().Be(0);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void SourceRefreshDoesNothingIfDisabled()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache, resortOnRefresh: false).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // when
                    SetRating(marketCache, markets[1], 2.0);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void SourceRefreshGeneratesUpdatesAsNeeded()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = ChangeSetByRating(marketCache).AsAggregator();
                    markets[0].Rating = 1.0;
                    marketCache.AddOrUpdate(markets);
                    markets[0].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    markets[1].SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));

                    // when
                    SetRating(marketCache, markets[1], 2.0);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Data.Items.Zip(markets[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                }

                [Fact]
                public void UpdatesToCorrectValueOnRefresh()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();
                    using var resultsLow = ChangeSetByLowRating(marketCache, false).AsAggregator();
                    using var resultsRefresh = ChangeSetByRating(marketCache, true).AsAggregator();
                    using var resultsLowRefresh = ChangeSetByLowRating(marketCache, true).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketBetter = new Market(1);
                    marketBetter.Rating = -1.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketBetter.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketBetter);

                    // when
                    SetRating(marketCache, marketBetter, 2.0);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    marketCacheResults.Summary.Overall.Refreshes.Should().Be(1);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                    resultsLow.Data.Count.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    resultsLow.Summary.Overall.Removes.Should().Be(0);
                    resultsLow.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                    resultsRefresh.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsRefresh.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    resultsRefresh.Summary.Overall.Removes.Should().Be(0);
                    resultsRefresh.Summary.Overall.Refreshes.Should().Be(0);
                    resultsRefresh.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                    resultsLowRefresh.Data.Count.Should().Be(PricesPerMarket);
                    resultsLowRefresh.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    resultsLowRefresh.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    resultsLowRefresh.Summary.Overall.Removes.Should().Be(0);
                    resultsLowRefresh.Summary.Overall.Refreshes.Should().Be(0);
                    resultsLowRefresh.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }
                [Fact]
                public void UpdatesToCorrectValueOnRemove()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x10012022);

                    // having
                    var marketOriginal = new Market(0);
                    var marketBetter = new Market(1);
                    var marketBest = new Market(2);
                    marketBetter.Rating = 1.0;
                    marketBest.Rating = 5.0;
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketBetter.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketBest.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketBest);
                    marketCache.AddOrUpdate(marketBetter);
                    using var results = ChangeSetByRating(marketCache, false).AsAggregator();

                    // when
                    marketCache.Remove(marketBest);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Refreshes.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
                }

                private static IObservable<IChangeSet<MarketPrice, int>> CreateChangeSet(
                        SourceCache<IMarket, Guid> marketCache,
                        string name,
                        IComparer<IMarket>? sourceComp = null,
                        IComparer<MarketPrice>? childCompare = null,
                        IEqualityComparer<MarketPrice>? equalityComparer = null,
                        bool resortOnRefresh = true) =>
                    marketCache.Connect()
                        .DebugSpy(name)
                        .MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"{name} [{m.Name} Prices]"), sourceComp ?? Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, equalityComparer, childCompare)
                        .DebugSpy($"{name} [Results]");

                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRating(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Rating",
                        resortOnRefresh: resortOnRefresh);
                
                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenHighPrice(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Rating | High",
                        sourceComp: Market.RatingCompare,
                        childCompare: MarketPrice.HighPriceCompare,
                        resortOnRefresh: resortOnRefresh);

                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenLowPrice(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Rating | Low",
                        sourceComp: Market.RatingCompare,
                        childCompare: MarketPrice.LowPriceCompare,
                        resortOnRefresh: resortOnRefresh);

                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenRecent(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Rating | Recent",
                        sourceComp: Market.RatingCompare,
                        childCompare: MarketPrice.LatestPriceCompare,
                        equalityComparer: MarketPrice.EqualityComparer,
                        resortOnRefresh: resortOnRefresh);
                
                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenTimeStamp(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Rating | Timestamp",
                        sourceComp: Market.RatingCompare,
                        childCompare: MarketPrice.LatestPriceCompare,
                        equalityComparer: MarketPrice.EqualityComparerWithTimeStamp,
                        resortOnRefresh: resortOnRefresh);

                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRating(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Low Rating",
                        sourceComp: Market.RatingCompare.Invert(),
                        resortOnRefresh: resortOnRefresh);
                
                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRatingThenHighPrice(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Low Rating | High",
                        sourceComp: Market.RatingCompare.Invert(),
                        childCompare: MarketPrice.HighPriceCompare,
                        resortOnRefresh: resortOnRefresh);
                
                private static IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRatingThenLowPrice(
                        SourceCache<IMarket, Guid> marketCache,
                        bool resortOnRefresh = true)
                    => CreateChangeSet(
                        marketCache: marketCache,
                        name: "Low Rating | Low",
                        sourceComp: Market.RatingCompare.Invert(),
                        childCompare: MarketPrice.LowPriceCompare,
                        resortOnRefresh: resortOnRefresh);

                private static IMarket SetRating(
                    SourceCache<IMarket, Guid> marketCache,
                    IMarket market,
                    double newRating)
                {
                    market.Rating = newRating;
                    marketCache.Refresh(market);
                    return market;
                }

            }
        }
    }
}
