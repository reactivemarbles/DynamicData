using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public sealed class MergeManyCacheChangeSetsFixture : IDisposable
{
    // const int MarketCount = 101;
    // const int PricesPerMarket = 103;
    // const int RemoveCount = 53;
    const int MarketCount = 3;
    const int PricesPerMarket = 5;
    const int RemoveCount = 2;
    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    private static readonly Random Random = new Random(0x10012023);

    private readonly ISourceList<IMarket> _marketList = new SourceList<IMarket>();

    private readonly ChangeSetAggregator<IMarket> _marketListResults;

    public MergeManyCacheChangeSetsFixture()
    {
        _marketListResults = _marketList.Connect().AsAggregator();
    }

    [Fact]
    public void AbleToInvokeFactory()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m)
        {
            invoked = true;
            return m.LatestPrices;
        }
        using var sub = _marketList.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketList.Add(new Market(0));

        // then
        _marketListResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets((Func<IMarket, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets(_ => Observable.Return(ChangeSet<MarketPrice, int>.Empty), comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets((Func<IMarket, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
        Assert.Throws<ArgumentNullException>(() => ObservableListEx.MergeManyChangeSets<Market, MarketPrice, int>(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableListEx.MergeManyChangeSets<Market, MarketPrice, int>(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
    }

    [Fact]
    public void AbleToInvokeFactoryWithKey()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m)
        {
            invoked = true;
            return m.LatestPrices;
        }
        using var sub = _marketList.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketList.Add(new Market(0));

        // then
        _marketListResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets((Func<IMarket, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets<IMarket, MarketPrice, int>(_ => Observable.Return(ChangeSet<MarketPrice, int>.Empty), comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketList.Connect().MergeManyChangeSets((Func<IMarket, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
        Assert.Throws<ArgumentNullException>(() => ObservableListEx.MergeManyChangeSets(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableListEx.MergeManyChangeSets(null!, (Func<Market, IObservable<IChangeSet<MarketPrice, int>>>)null!, null!, null!));
    }

    [Fact]
    public void AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        _marketList.AddRange(markets);

        // then
        _marketListResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);

        // when
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // then
        _marketListResults.Data.Count.Should().Be(MarketCount);
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
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        markets.ForEach(m => m.RefreshAllPrices(Random));

        // then
        _marketListResults.Data.Count.Should().Be(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);

        // when
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // then
        _marketListResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        markets[1].RemoveAllPrices();

        // then
        _marketListResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        _marketList.Remove(markets[0]);

        // then
        _marketListResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Updates.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        markets[1].RefreshAllPrices(Random);

        // then
        _marketListResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        _marketListResults.Data.Count.Should().Be(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        _marketList.RemoveRange(0, RemoveCount);

        // then
        _marketListResults.Data.Count.Should().Be(MarketCount - RemoveCount);
        results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
    }

    [Fact]
    public void ChangingSourceByAddRemoveRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.AddRandomPrices(Random, 0, PricesPerMarket * 2);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.AddRandomPrices(Random, PricesPerMarket, PricesPerMarket * 3);

        // when
        _marketList.Add(otherMarket);
        _marketList.Remove(market);

        // then
        _marketListResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        results.Messages.Count.Should().Be(3);
        results.Messages[0].Adds.Should().Be(PricesPerMarket * 2);
        results.Messages[1].Adds.Should().Be(PricesPerMarket);
        results.Messages[2].Updates.Should().Be(PricesPerMarket);
        results.Messages[2].Removes.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
        results.Data.Items.Should().BeSubsetOf(otherMarket.PricesCache.Items);
        otherMarket.PricesCache.Items.Should().BeSubsetOf(results.Data.Items);
    }

    [Fact]
    public void ChangingSourceByRemoveAddRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.AddRandomPrices(Random, 0, PricesPerMarket * 2);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.AddRandomPrices(Random, PricesPerMarket, PricesPerMarket * 3);

        // when
        _marketList.Remove(market);
        _marketList.Add(otherMarket);

        // then
        _marketListResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        results.Messages.Count.Should().Be(3);
        results.Messages[0].Adds.Should().Be(PricesPerMarket * 2);
        results.Messages[1].Removes.Should().Be(PricesPerMarket * 2);
        results.Messages[2].Adds.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 4);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket * 2);
        results.Data.Items.Should().BeSubsetOf(otherMarket.PricesCache.Items);
        otherMarket.PricesCache.Items.Should().BeSubsetOf(results.Data.Items);
    }

    [Fact]
    public void ChangingSourceByReplaceRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.AddRandomPrices(Random, 0, PricesPerMarket * 2);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.AddRandomPrices(Random, PricesPerMarket, PricesPerMarket * 3);

        // when
        _marketList.Replace(market, otherMarket);

        // then
        _marketListResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        //results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        //results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        //results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
        results.Data.Items.Should().BeSubsetOf(otherMarket.PricesCache.Items);
        otherMarket.PricesCache.Items.Should().BeSubsetOf(results.Data.Items);
    }

    [Fact]
    public void ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);

        // when
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // then
        _marketListResults.Data.Count.Should().Be(3);
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
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList.Add(marketOriginal);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);

        // then
        _marketListResults.Data.Count.Should().Be(3);
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
    public void ComparerOnlyAddsBetterValuesOnSourceReplace()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().DebugSpy("List").MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).DebugSpy("MergedLow").AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowLow = new Market(marketLow);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketLowLow.UpdatePrices(0, PricesPerMarket, LowestPrice - 1);
        _marketList.Insert(0, marketOriginal);
        _marketList.Insert(1, marketLow);

        // when
        _marketList.ReplaceAt(1, marketLowLow);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketFlipFlop);

        // when
        marketFlipFlop.RefreshAllPrices(LowestPrice);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketList.Remove(marketLow);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketFlipFlop);

        // when
        marketFlipFlop.UpdateAllPrices(LowestPrice);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);

        // when
        marketLow.UpdateAllPrices(LowestPrice - 1);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);

        // when
        marketLow.RefreshAllPrices(LowestPrice - 1);

        // then
        _marketListResults.Data.Count.Should().Be(2);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(market);

        // when
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);

        // then
        _marketListResults.Data.Count.Should().Be(1);
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
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices).AsAggregator();
        DisposeMarkets();

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
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
        var hasSourceSequenceCompleted = false;
        var hasMergedSequenceCompleted = false;

        using var cleanup = _marketList.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

        // when
        if (completeSource)
        {
            DisposeMarkets();
        }

        // then
        hasSourceSequenceCompleted.Should().Be(completeSource);
        hasMergedSequenceCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void MergedObservableWillFailIfSourceFails()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        _marketList.AddRange(markets);
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<IMarket>>(expectedError);

        using var cleanup = _marketList.Connect().Concat(throwObservable)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        receivedError.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _marketListResults.Dispose();
        DisposeMarkets();
    }

    private void DisposeMarkets()
    {
        _marketList.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketList.Dispose();
        _marketList.Clear();
    }

    private interface IMarket
    {
        public string Name { get; }

        public Guid Id { get; }

        public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }
    }

    private class Market : IMarket, IDisposable
    {
        private readonly ISourceCache<MarketPrice, int> _latestPrices = new SourceCache<MarketPrice, int>(p => p.ItemId);
        public static IComparer<IMarket> NameComparer { get; } = new NameComparerImpl();

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

        public IObservable<IChangeSet<MarketPrice, int>> LatestPrices => _latestPrices.Connect();

        public ISourceCache<MarketPrice, int> PricesCache => _latestPrices;

        public MarketPrice CreatePrice(int itemId, decimal price) => new(itemId, price, Id);

        public Market AddRandomIdPrices(Random r, int count, int minId, int maxId)
        {
            _latestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => CreatePrice(id, RandomPrice(r))));
            return this;
        }

        public Market AddRandomPrices(Random r, int minId, int maxId)
        {
            _latestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, RandomPrice(r))));
            return this;
        }

        public Market AddUniquePrices(Random r, int section, int count) => AddRandomPrices(r, section * ItemIdStride, (section * ItemIdStride) + count);

        public Market RefreshAllPrices(decimal newPrice)
        {
            _latestPrices.Edit(updater => updater.Items.ForEach(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

            return this;
        }

        public Market RefreshAllPrices(Random r) => RefreshAllPrices(RandomPrice(r));

        public Market RefreshPrice(int id, decimal newPrice)
        {
            _latestPrices.Edit(updater => updater.Lookup(id).IfHasValue(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));
            return this;
        }

        public void RemoveAllPrices() => this.With(_ => _latestPrices.Clear());

        public void RemovePrice(int itemId) => this.With(_ => _latestPrices.Remove(itemId));

        public Market UpdateAllPrices(decimal newPrice) => this.With(_ => _latestPrices.Edit(updater => updater.AddOrUpdate(updater.Items.Select(cp => CreatePrice(cp.ItemId, newPrice)))));

        public Market UpdatePrices(int minId, int maxId, decimal newPrice) => this.With(_ => _latestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, newPrice))));

        public void Dispose() => _latestPrices.Dispose();

        private class NameComparerImpl : IComparer<IMarket>
        {
            public int Compare([DisallowNull] IMarket x, [DisallowNull] IMarket y)
            {
                return x.Name.CompareTo(y.Name);
            }
        }
    }

    private static decimal RandomPrice(Random r) => BasePrice + ((decimal)r.NextDouble() * PriceOffset);

    private class MarketPrice
    {
        public static IEqualityComparer<MarketPrice> EqualityComparer { get; } = new CurrentPriceEqualityComparer();
        public static IEqualityComparer<MarketPrice> EqualityComparerWithTimeStamp { get; } = new TimeStampPriceEqualityComparer();
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

        public override string ToString() => $"{ItemId:D5} - {Price:c} ({MarketId}) [{TimeStamp:HH:mm:ss.fffffff}]";

        private class CurrentPriceEqualityComparer : IEqualityComparer<MarketPrice>
        {
            public virtual bool Equals([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y) => x.MarketId.Equals(x.MarketId) && (x.ItemId == y.ItemId) && (x.Price == y.Price);
            public int GetHashCode([DisallowNull] MarketPrice obj) => throw new NotImplementedException();
        }

        private class TimeStampPriceEqualityComparer : CurrentPriceEqualityComparer, IEqualityComparer<MarketPrice>
        {
            public override bool Equals([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y) => base.Equals(x, y) && (x.TimeStamp == y.TimeStamp);
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
                return y.TimeStamp.CompareTo(x.TimeStamp);
            }
        }
    }

    private class FixedMarket : IMarket
    {
        public FixedMarket(Random r, int minId, int maxId, bool completable = true)
        {
            Id = Guid.NewGuid();
            LatestPrices = Enumerable.Range(minId, maxId - minId)
                                    .Select(id => new MarketPrice(id, RandomPrice(r), Id))
                                    .AsObservableChangeSet(cp => cp.ItemId, completable: completable);
        }

        public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }

        public string Name => Id.ToString("B");

        public Guid Id { get; }
    }

    class NoOpComparer<T> : IComparer<T>
    {
        public int Compare(T x, T y) => throw new NotImplementedException();
    }

    class NoOpEqualityComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T x, T y) => throw new NotImplementedException();
        public int GetHashCode([DisallowNull] T obj) => throw new NotImplementedException();
    }
}

internal static class Extensions
{
    public static T With<T>(this T item, Action<T> action)
    {
        action(item);
        return item;
    }

    public static IObservable<T> ForceFail<T>(this IObservable<T> source, int count, Exception? e) =>
        (e is not null)
            ? source.Take(count).Concat(Observable.Throw<T>(e))
            : source;
}
