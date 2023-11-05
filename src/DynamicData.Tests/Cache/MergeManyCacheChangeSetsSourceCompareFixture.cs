using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;

using Xunit;
using Xunit.Sdk;

namespace DynamicData.Tests.Cache;

public sealed class MergeManyCacheChangeSetsSourceCompareFixture : IDisposable
{
    const int MarketCount = 5;
    const int PricesPerMarket = 7;
    //const int MarketCount = 101;
    //const int PricesPerMarket = 103;
    const int RemoveCount = 53;
    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    private static readonly Random Random = new Random(0x21123737);

    private static decimal GetRandomPrice() => MarketPrice.RandomPrice(Random, BasePrice, PriceOffset);

    private readonly ISourceCache<IMarket, Guid> _marketCache = new SourceCache<IMarket, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<IMarket, Guid> _marketCacheResults;

    public MergeManyCacheChangeSetsSourceCompareFixture()
    {
        _marketCacheResults = _marketCache.Connect().AsAggregator();
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
        var actionParentCompareKey2c = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: nullParentComparer, equalityComparer: emptyEqualityComparer);
        var actionParentCompareKey2d = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: emptyParentComparer, equalityComparer: nullEqualityComparer);

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
        actionParentCompareKey2c.Should().Throw<ArgumentNullException>();
        actionParentCompareKey2d.Should().Throw<ArgumentNullException>();
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
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AbleToInvokeFactoryWithKey()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m, Guid g)
        {
            invoked = true;
            return m.LatestPrices;
        }
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
    }

#if false
    [Fact]
    public void AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        _marketCache.AddOrUpdate(markets);

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        markets.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets[0].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RemoveAllPrices();

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketCache.Remove(markets[0]);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].AddRandomPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RefreshAllPrices(GetRandomPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.AddRandomPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketCache.AddOrUpdate(market);
        var updatedMarket = new Market(market);
        updatedMarket.AddRandomPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketCache.AddOrUpdate(updatedMarket);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(updatedMarket.PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }
#endif

    [Fact]
    public void ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var results = ChangeSetByRatingOnly(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);

        // when
        _marketCache.AddOrUpdate(marketBetter);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void ComparerOnlyAddsBetterExistingValues()
    {
        // having
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        using var results = ChangeSetByRatingOnly(false).DebugSpy("Rating").AsAggregator();

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void ComparerOnlyAddsBetterValuesOnSourceUpdate()
    {
        // having
        using var results = ChangeSetByRatingOnly(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void ComparerUpdatesToCorrectValueOnRefresh()
    {
        // having
        using var results = ChangeSetByRatingOnly(true).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        SetRating(marketBetter, 1.0);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void ComparerUpdatesToCorrectValueOnRemove()
    {
        // having
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        var marketBest = new Market(2);
        marketBetter.Rating = 1.0;
        marketBest.Rating = 5.0;
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBest.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBest);
        _marketCache.AddOrUpdate(marketBetter);
        using var results = ChangeSetByRatingOnly(false).DebugSpy("Results").AsAggregator();

        // when
        _marketCache.Remove(marketBest);

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void ComparerUpdatesToCorrectValueOnUpdate()
    {
        // having
        using var results = ChangeSetByRatingOnly(false).DebugSpy("Rating").AsAggregator();
        using var resultsLow = ChangeSetByLowRating(false).DebugSpy("Rating Low").AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        marketBetter.UpdateAllPrices(GetRandomPrice());

        // then
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

#if false
    [Fact]
    public void ComparerOnlyUpdatesVisibleValuesOnUpdate()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(0, PricesPerMarket, GetRandomPrice);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
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
#endif

    [Fact]
    public void EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).AsAggregator();
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
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
        var hasSourceSequenceCompleted = false;
        var hasMergedSequenceCompleted = false;

        using var cleanup = _marketCache.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
                            .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

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
        _marketCache.AddOrUpdate(markets);
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<IMarket, Guid>>(expectedError);

        using var cleanup = _marketCache.Connect().Concat(throwObservable)
                            .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        receivedError.Should().Be(expectedError);
    }

    private IObservable<IChangeSet<MarketPrice, int>> CreateChangeSet(string name, IComparer<IMarket>? sourceComp = null, IComparer<MarketPrice>? childCompare = null, IEqualityComparer<MarketPrice>? equalityComparer = null, bool resortOnRefresh = true) =>
        _marketCache.Connect()
            .DebugSpy(name)
            .MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"{name} [{m.Name} Prices]"), sourceComp ?? Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, equalityComparer, childCompare)
            .DebugSpy($"{name} [Results]");

    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingOnly(bool resortOnRefresh = true) => CreateChangeSet("Rating", resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRating(bool resortOnRefresh = true) => CreateChangeSet("Rating Low", Market.RatingCompare.Invert(), resortOnRefresh: resortOnRefresh);
    //private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenHigh(bool resortOnRefresh = true) => _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"Rating / High [{m.Name}]"), Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, MarketPrice.HighPriceCompare);
    //private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenLow(bool resortOnRefresh = true) => _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"Rating / Low [{m.Name}]"), Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, MarketPrice.LowPriceCompare);
    //private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenRecent(bool resortOnRefresh = true) => _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"Rating / Recent [{m.Id}]"), Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, MarketPrice.EqualityComparerWithTimeStamp, MarketPrice.LatestPriceCompare);
    //private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenRecentNoTimestamp(bool resortOnRefresh = true) => _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"Rating / Recent (No TS) [{m.Id}]"), Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, MarketPrice.EqualityComparer, MarketPrice.LatestPriceCompare);

    private IMarket SetRating(IMarket market, double newRating)
    {
        market.Rating = newRating;
        _marketCache.Refresh(market);
        return market;
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        DisposeMarkets();
    }

    private void DisposeMarkets()
    {
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
        _marketCache.Clear();
    }
}
