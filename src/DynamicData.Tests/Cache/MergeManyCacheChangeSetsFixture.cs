using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class MergeManyCacheChangeSetsFixture : IDisposable
{
    const int MarketCount = 101;
    const int PricesPerMarket = 103;
    const int RemoveCount = 53;
    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    private static readonly Random Random = new Random(0x21123737);

    private readonly ISourceCache<Market, Guid> _marketCache = new SourceCache<Market, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<Market, Guid> _marketCacheResults;

    public MergeManyCacheChangeSetsFixture()
    {
        _marketCacheResults = _marketCache.Connect().AsAggregator();
    }

    [Fact]
    public void AbleToInvokeFactory()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(Market m)
        {
            invoked = true;
            return m.LatestPrices.Connect();
        }
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets((Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets(_ => Observable.Return(ChangeSet<MarketPrice, int>.Empty), comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets((Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeManyChangeSets<Market, Guid, MarketPrice, int>(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeManyChangeSets<Market, Guid, MarketPrice, int>(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
    }

    [Fact]
    public void AbleToInvokeFactoryWithKey()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(Market m, Guid g)
        {
            invoked = true;
            return m.LatestPrices.Connect();
        }
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets((Func<Market, Guid, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets((_, _) => Observable.Return(ChangeSet<MarketPrice, int>.Empty), comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeManyChangeSets((Func<Market, Guid, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeManyChangeSets(null!, (Func<Market, Guid, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeManyChangeSets(null!, (Func<Market, Guid, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
    }

    [Fact]
    public void AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        _marketCache.AddOrUpdate(markets);

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.LatestPrices.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AllNewSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.LatestPrices.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AllRefreshedSubItemsAreRefreshed()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        markets.ForEach(m => m.RefreshAllPrices(Random));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
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
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].LatestPrices.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        markets[1].RemoveAllPrices();

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].LatestPrices.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        _marketCache.Remove(markets[0]);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[1].LatestPrices.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Updates.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        markets[1].RefreshAllPrices(Random);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(markets[0].LatestPrices.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        markets.ForEach(m => m.LatestPrices.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
        results.Messages.Count.Should().Be(MarketCount * 2);
        results.Messages[0].Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
    }

    [Fact]
    public void AnySourceItemRemovedRemovesAllSourceValues()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        _marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount - RemoveCount);
        results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
    }

    [Fact]
    public void ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.AddRandomPrices(Random, 0, PricesPerMarket * 2);
        _marketCache.AddOrUpdate(market);
        var updatedMarket = new Market(market);
        updatedMarket.AddRandomPrices(Random, PricesPerMarket, PricesPerMarket * 3);

        // when
        _marketCache.AddOrUpdate(updatedMarket);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(updatedMarket.LatestPrices.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // when
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(3);
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
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketCache.AddOrUpdate(marketOriginal);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // then
        _marketCacheResults.Data.Count.Should().Be(3);
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
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowLow = new Market(marketLow);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketLowLow.UpdatePrices(0, PricesPerMarket, LowestPrice - 1);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        _marketCache.AddOrUpdate(marketLowLow);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
    public void ComparerUpdatesToCorrectValueOnRefresh()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketFlipFlop);

        // when
        marketFlipFlop.RefreshAllPrices(LowestPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
        // having
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketCache.Remove(marketLow);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketFlipFlop);

        // when
        marketFlipFlop.UpdateAllPrices(LowestPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
    public void ComparerOnlyUpdatesVisibleValuesOnUpdate()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        marketLow.UpdateAllPrices(LowestPrice - 1);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
    public void ComparerOnlyRefreshesVisibleValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        marketLow.RefreshAllPrices(LowestPrice - 1);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
    public void EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = new Market(0);
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.Connect(), MarketPrice.EqualityComparer).AsAggregator();
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(market);

        // when
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void ObservableCompletesWhenSourceAndAllChildrenComplete()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n*ItemIdStride, (n*ItemIdStride) + PricesPerMarket)).ToArray();
        var hasMainSequenceCompleted = false;
        var hasChildSequenceCompleted = false;

        var observable = markets.AsObservableChangeSet(m => m.MarketId, completable: true)
                                                                .Do(_ => { }, () => hasMainSequenceCompleted = true)
                                                                .MergeManyChangeSets(m => m.LatestPrices)
                                                                .Do(_ => { }, () => hasChildSequenceCompleted = true);

        // when
        using var cleanup = observable.Subscribe();

        // then
        hasMainSequenceCompleted.Should().BeTrue();
        hasChildSequenceCompleted.Should().BeTrue();
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        _marketCache.Items.ForEach(m => m.Dispose());
        _marketCache.Dispose();
    }

    private class Market : IDisposable
    {
        private Market(string name, Guid id)
        {
            Name = name;
            Id = id;
        }

        public Market(Market market) : this(market.Name, market.Id)
        {
        }

        public Market(int name) : this($"Market #{name}", Guid.NewGuid())
        {
        }

        public string Name { get; }

        public Guid Id { get; }

        public ISourceCache<MarketPrice, int> LatestPrices { get; } = new SourceCache<MarketPrice, int>(p => p.ItemId);

        public MarketPrice CreatePrice(int itemId, decimal price) => new MarketPrice(itemId, price, Id);

        public void AddRandomIdPrices(Random r, int count, int minId, int maxId) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => CreatePrice(id, RandomPrice(r))));

        public void AddRandomPrices(Random r, int minId, int maxId) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, RandomPrice(r))));

        public void RefreshAllPrices(decimal newPrice) =>
            LatestPrices.Edit(updater => updater.Items.ForEach(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

        public void RefreshAllPrices(Random r) => RefreshAllPrices(RandomPrice(r));

        public void RefreshPrice(int id, decimal newPrice) =>
            LatestPrices.Edit(updater => updater.Lookup(id).IfHasValue(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

        public void RemoveAllPrices() => LatestPrices.Clear();

        public void RemovePrice(int itemId) => LatestPrices.Remove(itemId);

        public void UpdateAllPrices(decimal newPrice) =>
            LatestPrices.Edit(updater => updater.AddOrUpdate(updater.Items.Select(cp => CreatePrice(cp.ItemId, newPrice))));

        public void UpdatePrices(int minId, int maxId, decimal newPrice) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, newPrice)));

        public void Dispose() => LatestPrices.Dispose();
    }

    private static decimal RandomPrice(Random r) => BasePrice + ((decimal)r.NextDouble() * PriceOffset);

    private class MarketPrice
    {
        public static IEqualityComparer<MarketPrice> EqualityComparer { get; } = new CurrentPriceEqualityComparer();
        public static IComparer<MarketPrice> HighPriceCompare { get; } = new HighestPriceComparer();
        public static IComparer<MarketPrice> LowPriceCompare { get; } = new LowestPriceComparer();
        public static IComparer<MarketPrice> LatestPriceCompare { get; } = new LatestPriceComparer();

        private decimal _price;

        public MarketPrice(int itemId, decimal price, Guid marketId)
        {
            ItemId = itemId;
            MarketId = marketId;
            Price = price;
        }

        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                TimeStamp = DateTimeOffset.UtcNow;
            }
        }

        public DateTimeOffset TimeStamp { get; private set; }

        public Guid MarketId { get; }

        public int ItemId { get; }

        private class CurrentPriceEqualityComparer : IEqualityComparer<MarketPrice>
        {
            public bool Equals([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y) => x.MarketId.Equals(x.MarketId) && (x.ItemId == y.ItemId) && (x.Price == y.Price);
            public int GetHashCode([DisallowNull] MarketPrice obj) => throw new NotImplementedException();
        }

        private class LowestPriceComparer : IComparer<MarketPrice>
        {
            public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
            {
                Debug.Assert(x.ItemId == y.ItemId);
                return x.Price.CompareTo(y.Price);
            }
        }

        private class HighestPriceComparer : IComparer<MarketPrice>
        {
            public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
            {
                Debug.Assert(x.ItemId == y.ItemId);
                return y.Price.CompareTo(x.Price);
            }
        }

        private class LatestPriceComparer : IComparer<MarketPrice>
        {
            public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
            {
                Debug.Assert(x.ItemId == y.ItemId);
                return x.TimeStamp.CompareTo(y.TimeStamp);
            }
        }
    }

    private class FixedMarket
    {
        public FixedMarket(Random r, int minId, int maxId)
        {
            MarketId = Guid.NewGuid();
            LatestPrices = Enumerable.Range(minId, maxId - minId)
                                    .Select(id => new MarketPrice(id, RandomPrice(r), MarketId))
                                    .AsObservableChangeSet(cp => cp.ItemId, completable: true);
        }

        public Guid MarketId { get; }

        public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }
    }

}
