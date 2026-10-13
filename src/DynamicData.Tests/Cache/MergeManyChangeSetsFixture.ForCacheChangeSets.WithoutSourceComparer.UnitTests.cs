namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForCacheChangeSets
    {
        public static partial class WithoutSourceComparer
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
                    using var sub = marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

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
                    using var sub = marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

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

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    AddUniquePrices(markets, randomizer);

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
                }

                [Fact]
                public void AllNewSubItemsPresentInResult()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    marketCache.AddOrUpdate(markets);

                    // when
                    AddUniquePrices(markets, randomizer);

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
                    results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
                    results.Messages.Count.Should().Be(MarketCount);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(0);
                    results.Summary.Overall.Updates.Should().Be(0);
                }

                [Fact]
                public void AllRefreshedSubItemsAreRefreshed()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    marketCache.AddOrUpdate(markets);
                    AddUniquePrices(markets, randomizer);

                    // when
                    markets.ForEach(m => m.RefreshAllPrices(() => GetRandomPrice(randomizer)));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
                    results.Messages.Count.Should().Be(MarketCount * 2);
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

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
                }

                [Fact]
                public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
                }

                [Fact]
                public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
                }

                [Fact]
                public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
                }

                [Fact]
                public void AnyRemovedSubItemIsRemoved()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    marketCache.AddOrUpdate(markets);
                    AddUniquePrices(markets, randomizer);

                    // when
                    markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount);
                    results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
                    results.Messages.Count.Should().Be(MarketCount * 2);
                    results.Messages[0].Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
                }

                [Fact]
                public void AnySourceItemRemovedRemovesAllSourceValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    AddUniquePrices(markets, randomizer);
                    marketCache.AddOrUpdate(markets);

                    // when
                    marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

                    // then
                    marketCacheResults.Data.Count.Should().Be(MarketCount - RemoveCount);
                    results.Messages.Count.Should().Be(2);
                    results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
                }

                [Fact]
                public void ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
                    results.Data.Items.Zip(updatedMarket.PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
                }

                [Fact]
                public void ClearingParentEmitsSingleChangeSet()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    AddUniquePrices(markets, randomizer);
                    marketCache.AddOrUpdate(markets);

                    // when
                    marketCache.Clear();

                    // then
                    marketCacheResults.Data.Count.Should().Be(0);
                    results.Data.Count.Should().Be(0);
                    results.Messages.Count.Should().Be(2);
                    results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(MarketCount * PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(0);
                }

                [Fact]
                public void ComparerOnlyAddsBetterAddedValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketHigh = new Market(2);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(marketHigh);

                    // when
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(3);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketHigh.Id));
                }

                [Fact]
                public void ComparerOnlyAddsBetterExistingValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketHigh = new Market(2);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

                    // when
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(marketHigh);

                    // then
                    marketCacheResults.Data.Count.Should().Be(3);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketHigh.Id));
                }

                [Fact]
                public void ComparerOnlyAddsBetterValuesOnSourceUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketLowLow = new Market(marketLow);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketLowLow.SetPrices(0, PricesPerMarket, LowestPrice - 1);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);

                    // when
                    marketCache.AddOrUpdate(marketLowLow);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowLow.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(0);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void ComparerOnlyRefreshesVisibleValues()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);

                    // when
                    marketLow.RefreshAllPrices(LowestPrice - 1);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(0);
                    highPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void ComparerOnlyUpdatesVisibleValuesOnUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);

                    // when
                    marketLow.UpdateAllPrices(LowestPrice - 1);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    lowPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(0);
                    highPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void ComparerUpdatesToCorrectValueOnRefresh()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketFlipFlop = new Market(1);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketFlipFlop);

                    // when
                    marketFlipFlop.RefreshAllPrices(LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketFlipFlop.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    highPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void ComparerUpdatesToCorrectValueOnRemove()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketLow = new Market(1);
                    var marketHigh = new Market(2);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketLow);
                    marketCache.AddOrUpdate(marketHigh);
                    marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
                    marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

                    // when
                    marketCache.Remove(marketLow);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    results.Data.Count.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Updates.Should().Be(0);
                    results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketHigh.Id));
                }

                [Fact]
                public void ComparerUpdatesToCorrectValueOnUpdate()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    using var highPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
                    using var lowPriceResults = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
                    var marketOriginal = new Market(0);
                    var marketFlipFlop = new Market(1);
                    marketOriginal.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer));
                    marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
                    marketCache.AddOrUpdate(marketOriginal);
                    marketCache.AddOrUpdate(marketFlipFlop);

                    // when
                    marketFlipFlop.UpdateAllPrices(LowestPrice);

                    // then
                    marketCacheResults.Data.Count.Should().Be(2);
                    lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Removes.Should().Be(0);
                    lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
                    lowPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    lowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketFlipFlop.Id));
                    highPriceResults.Data.Count.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    highPriceResults.Summary.Overall.Removes.Should().Be(0);
                    highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
                    highPriceResults.Summary.Overall.Refreshes.Should().Be(0);
                    highPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
                }

                [Fact]
                public void EqualityComparerHidesUpdatesWithoutChanges()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);
                    using var marketCacheResults = marketCache.Connect().AsAggregator();

                    // having
                    var market = new Market(0);
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(() => GetRandomPrice(randomizer), n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

                    // when
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices).AsAggregator();

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

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(() => GetRandomPrice(randomizer), n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
                    var hasSourceSequenceCompleted = false;
                    var hasMergedSequenceCompleted = false;

                    using var cleanup = marketCache.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
                        .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

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

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    marketCache.AddOrUpdate(markets);
                    var receivedError = default(Exception);
                    var expectedError = new Exception("Test exception");
                    var throwObservable = Observable.Throw<IChangeSet<IMarket, Guid>>(expectedError);

                    using var cleanup = marketCache.Connect().Concat(throwObservable)
                        .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, err => receivedError = err);

                    // when
                    marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
                    marketCache.Dispose();

                    // then
                    receivedError.Should().Be(expectedError);
                }

                [Fact]
                public void MergeManyChangeSetsWorksCorrectlyWithValueTypes()
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);

                    var randomizer = new Randomizer(0x21123737);

                    // having
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    marketCache.AddOrUpdate(markets);
                    markets.ForEach(m => m.SetPrices(0, PricesPerMarket, () => GetRandomPrice(randomizer)));
                    using var results = marketCache.Connect()
                        .MergeManyChangeSets(m => m.LatestPrices.Transform(p => p.Price))
                        .AsAggregator();

                    // when
                    markets.ForEach(m => m.RemoveAllPrices());

                    // then
                    results.Data.Count.Should().Be(0);
                    results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
                    results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
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
                    var actionDefault1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector);
                    var actionDefault2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector);
                    var actionDefault2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector);
                    var actionChildCompare1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, comparer: emptyChildComparer);
                    var actionChildCompare2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, comparer: emptyChildComparer);
                    var actionChildCompare2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, comparer: emptyChildComparer);
                    var actionChildCompare2c = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, comparer: nullChildComparer);

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

                    actionDefault1.Should().Throw<ArgumentNullException>();
                    actionDefault2a.Should().Throw<ArgumentNullException>();
                    actionDefault2b.Should().Throw<ArgumentNullException>();
                    actionChildCompare1.Should().Throw<ArgumentNullException>();
                    actionChildCompare2a.Should().Throw<ArgumentNullException>();
                    actionChildCompare2b.Should().Throw<ArgumentNullException>();
                    actionChildCompare2c.Should().Throw<ArgumentNullException>();
                }

                [Theory]
                [InlineData(true)]
                [InlineData(false)]
                public void OrderOfChangesIsPreserved(bool removeFirst)
                {
                    using var marketCache = new SourceCache<IMarket, Guid>(p => p.Id);

                    var randomizer = new Randomizer(0x21123737);

                    // Arrange
                    var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    AddUniquePrices(markets, randomizer);
                    marketCache.AddOrUpdate(markets);
                    var markets2 = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
                    AddUniquePrices(markets2, randomizer);
                    using var results = marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
                    (var firstReason, var nextReason, int expectedChanges) = removeFirst 
                        ? (ChangeReason.Remove, ChangeReason.Add, 2 * MarketCount * PricesPerMarket)
                        : (ChangeReason.Add, ChangeReason.Remove, 3 * MarketCount * PricesPerMarket);

                    // Act
                    marketCache.Edit(updater =>
                    {
                        if (removeFirst)
                        {
                            updater.Clear();
                            updater.AddOrUpdate(markets2);
                        }
                        else
                        {

                            updater.AddOrUpdate(markets2);
                            updater.Clear();
                        }
                    });

                    // Assert
                    results.Messages.Count.Should().Be(2);
                    results.Messages[0].All(change => change.Reason is ChangeReason.Add).Should().BeTrue();
                    results.Messages[1].Count.Should().Be(expectedChanges);
                    results.Messages[1].Take(MarketCount * PricesPerMarket).All(change => change.Reason == firstReason).Should().BeTrue();
                    results.Messages[1].Skip(MarketCount * PricesPerMarket).All(change => change.Reason == nextReason).Should().BeTrue();
                }

                private static void AddUniquePrices(
                        Market[] markets,
                        Randomizer randomizer)
                    => markets.ForEach(m => m.AddUniquePrices(PricesPerMarket, _ => GetRandomPrice(randomizer)));
            }
        }
    }
}
