using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public sealed partial class MergeChangeSetsFixture : IDisposable
{
#if DEBUG
    const int MarketCount = 5;
    const int PricesPerMarket = 7;
    const int RemoveCount = 3;
#else
    const int MarketCount = 101;
    const int PricesPerMarket = 103;
    const int RemoveCount = 53;
#endif

    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    public static readonly Random Random = new(0x12291977);

    private readonly List<Market> _marketList = new();

    private static decimal GetRandomPrice() => MarketPrice.RandomPrice(Random, BasePrice, PriceOffset);

    public MergeChangeSetsFixture()
    {
    }

    [Fact]
    public void NullChecks()
    {
        // having
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int, int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int, int>>)null!;
        var emptyChangeSetObsObs = Observable.Empty<IObservable<IChangeSet<int, int>>>();
        var nullChangeSetObsObs = (IObservable<IObservable<IChangeSet<int, int>>>)null!;
        var nullComparer = (IComparer<int>)null!;
        var nullEqualityComparer = (IEqualityComparer<int>)null!;
        var nullChangeSetObsEnum = (IEnumerable<IObservable<IChangeSet<int, int>>>)null!;
        var emptyChangeSetObsEnum = Enumerable.Empty<IObservable<IChangeSet<int, int>>>();
        var comparer = new NoOpComparer<int>();
        var equalityComparer = new NoOpEqualityComparer<int>();

        // when
        var obsobs = () => nullChangeSetObsObs.MergeChangeSets();
        var obsobsComp = () => nullChangeSetObsObs.MergeChangeSets(comparer);
        var obsobsComp1 = () => emptyChangeSetObsObs.MergeChangeSets(nullComparer);
        var obsobsEq = () => nullChangeSetObsObs.MergeChangeSets(equalityComparer);
        var obsobsEq1 = () => emptyChangeSetObsObs.MergeChangeSets(nullEqualityComparer);
        var obsobsEqComp = () => nullChangeSetObsObs.MergeChangeSets(equalityComparer, comparer);
        var obsobsEqComp1 = () => emptyChangeSetObsObs.MergeChangeSets(nullEqualityComparer, comparer);
        var obsobsEqComp2 = () => emptyChangeSetObsObs.MergeChangeSets(equalityComparer, nullComparer);

        var obspair = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObs);
        var obspairB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObs);
        var obspairComp = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObs, comparer);
        var obspairCompB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObs, comparer);
        var obspairComp1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObs, nullComparer);
        var obspairEq = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObs, equalityComparer);
        var obspairEqB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObs, equalityComparer);
        var obspairEq1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObs, nullEqualityComparer);
        var obspairEqComp = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObs, equalityComparer, comparer);
        var obspairEqCompB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObs, equalityComparer, comparer);
        var obspairEqComp1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObs, nullEqualityComparer, comparer);
        var obspairEqComp2 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObs, equalityComparer, nullComparer);

        var obsEnum = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum);
        var obsEnumB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObsEnum);
        var obsEnumComp = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, comparer);
        var obsEnumCompB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObsEnum, comparer);
        var obsEnumComp1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, nullComparer);
        var obsEnumEq = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, equalityComparer);
        var obsEnumEqB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObsEnum, equalityComparer);
        var obsEnumEq1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, nullEqualityComparer);
        var obsEnumEqComp = () => nullChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, equalityComparer, comparer);
        var obsEnumEqCompB = () => emptyChangeSetObs.MergeChangeSets(nullChangeSetObsEnum, equalityComparer, comparer);
        var obsEnumEqComp1 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, nullEqualityComparer, comparer);
        var obsEnumEqComp2 = () => emptyChangeSetObs.MergeChangeSets(emptyChangeSetObsEnum, equalityComparer, nullComparer);

        var enumObs = () => nullChangeSetObsEnum.MergeChangeSets();
        var enumObsComp = () => nullChangeSetObsEnum.MergeChangeSets(comparer);
        var enumObsComp1 = () => emptyChangeSetObsEnum.MergeChangeSets(nullComparer);
        var enumObsEq = () => nullChangeSetObsEnum.MergeChangeSets(equalityComparer);
        var enumObsEq1 = () => emptyChangeSetObsEnum.MergeChangeSets(nullEqualityComparer);
        var enumObsEqComp = () => nullChangeSetObsEnum.MergeChangeSets(equalityComparer, comparer);
        var enumObsEqComp1 = () => emptyChangeSetObsEnum.MergeChangeSets(nullEqualityComparer, comparer);
        var enumObsEqComp2 = () => emptyChangeSetObsEnum.MergeChangeSets(equalityComparer, nullComparer);

        // then
        emptyChangeSetObs.Should().NotBeNull();
        emptyChangeSetObsObs.Should().NotBeNull();
        emptyChangeSetObsEnum.Should().NotBeNull();
        comparer.Should().NotBeNull();
        equalityComparer.Should().NotBeNull();
        nullChangeSetObs.Should().BeNull();
        nullChangeSetObsObs.Should().BeNull();
        nullComparer.Should().BeNull();
        nullEqualityComparer.Should().BeNull();
        nullChangeSetObsEnum.Should().BeNull();

        obsobs.Should().Throw<ArgumentNullException>();
        obsobsComp.Should().Throw<ArgumentNullException>();
        obsobsComp1.Should().Throw<ArgumentNullException>();
        obsobsEq.Should().Throw<ArgumentNullException>();
        obsobsEq1.Should().Throw<ArgumentNullException>();
        obsobsEqComp.Should().Throw<ArgumentNullException>();
        obsobsEqComp1.Should().Throw<ArgumentNullException>();
        obsobsEqComp2.Should().Throw<ArgumentNullException>();
        obspair.Should().Throw<ArgumentNullException>();
        obspairB.Should().Throw<ArgumentNullException>();
        obspairComp.Should().Throw<ArgumentNullException>();
        obspairCompB.Should().Throw<ArgumentNullException>();
        obspairComp1.Should().Throw<ArgumentNullException>();
        obspairEq.Should().Throw<ArgumentNullException>();
        obspairEqB.Should().Throw<ArgumentNullException>();
        obspairEq1.Should().Throw<ArgumentNullException>();
        obspairEqComp.Should().Throw<ArgumentNullException>();
        obspairEqCompB.Should().Throw<ArgumentNullException>();
        obspairEqComp1.Should().Throw<ArgumentNullException>();
        obspairEqComp2.Should().Throw<ArgumentNullException>();
        obsEnum.Should().Throw<ArgumentNullException>();
        obsEnumB.Should().Throw<ArgumentNullException>();
        obsEnumComp.Should().Throw<ArgumentNullException>();
        obsEnumCompB.Should().Throw<ArgumentNullException>();
        obsEnumComp1.Should().Throw<ArgumentNullException>();
        obsEnumEq.Should().Throw<ArgumentNullException>();
        obsEnumEqB.Should().Throw<ArgumentNullException>();
        obsEnumEq1.Should().Throw<ArgumentNullException>();
        obsEnumEqComp.Should().Throw<ArgumentNullException>();
        obsEnumEqCompB.Should().Throw<ArgumentNullException>();
        obsEnumEqComp1.Should().Throw<ArgumentNullException>();
        obsEnumEqComp2.Should().Throw<ArgumentNullException>();
        enumObs.Should().Throw<ArgumentNullException>();
        enumObsComp.Should().Throw<ArgumentNullException>();
        enumObsComp1.Should().Throw<ArgumentNullException>();
        enumObsEq.Should().Throw<ArgumentNullException>();
        enumObsEq1.Should().Throw<ArgumentNullException>();
        enumObsEqComp.Should().Throw<ArgumentNullException>();
        enumObsEqComp1.Should().Throw<ArgumentNullException>();
        enumObsEqComp2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AllExistingItemsPresentInResult()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
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
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // when
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
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
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        _marketList.Count.Should().Be(MarketCount);
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
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();

        // when
        _marketList[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        _marketList.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(_marketList[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketList[1].RemoveAllPrices();

        // then
        _marketList.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(_marketList[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketList[0].RemoveAllPrices();

        // then
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(_marketList[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Updates.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketList[1].RefreshAllPrices(GetRandomPrice);

        // then
        _marketList.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(_marketList[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void AnyRemovedSubItemIsRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount).ToList())));

        // then
        _marketList.Count.Should().Be(MarketCount);
        results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
        results.Messages.Count.Should().Be(MarketCount * 2);
        results.Messages[0].Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
    }

    [Fact]
    public void ComparerOnlyAddsBetterAddedValues()
    {
        // having
        var marketOriginal = Add(new Market(0));
        var marketLow = Add(new Market(1));
        var marketHigh = Add(new Market(2));
        var others = new[] { marketLow.LatestPrices, marketHigh.LatestPrices };
        using var highPriceResults = marketOriginal.LatestPrices.MergeChangeSets(others, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = marketOriginal.LatestPrices.MergeChangeSets(others, MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // then
        _marketList.Count.Should().Be(3);
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
        var marketOriginal = Add(new Market(0));
        var marketLow = Add(new Market(1));
        var marketHigh = Add(new Market(2));
        var others = new[] { marketLow.LatestPrices, marketHigh.LatestPrices };
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        using var highPriceResults = marketOriginal.LatestPrices.MergeChangeSets(others, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = marketOriginal.LatestPrices.MergeChangeSets(others, MarketPrice.LowPriceCompare).AsAggregator();

        // then
        _marketList.Count.Should().Be(3);
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
    public void ComparerUpdatesToCorrectValueOnRefresh()
    {
        // having
        var marketOriginal = Add(new Market(0));
        var marketFlipFlop = Add(new Market(1));
        using var highPriceResults = marketOriginal.LatestPrices.MergeChangeSets(marketFlipFlop.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = marketOriginal.LatestPrices.MergeChangeSets(marketFlipFlop.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        marketFlipFlop.RefreshAllPrices(LowestPrice);

        // then
        _marketList.Count.Should().Be(2);
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
        var marketOriginal = Add(new Market(0));
        var marketLow = Add(new Market(1));
        var marketHigh = Add(new Market(2));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        marketLow.RemoveAllPrices();

        // then
        _marketList.Count.Should().Be(3);
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
        var marketOriginal = Add(new Market(0));
        var marketFlipFlop = Add(new Market(1));
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        marketFlipFlop.UpdateAllPrices(LowestPrice);

        // then
        _marketList.Count.Should().Be(2);
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
        var marketOriginal = Add(new Market(0));
        var marketLow = Add(new Market(1));
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        marketLow.UpdateAllPrices(LowestPrice - 1);

        // then
        _marketList.Count.Should().Be(2);
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
        var marketOriginal = Add(new Market(0));
        var marketLow = Add(new Market(1));
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        marketLow.RefreshAllPrices(LowestPrice - 1);

        // then
        _marketList.Count.Should().Be(2);
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
    public void EnumObservableUsesTheScheduler()
    {
        // having
        var scheduler = new TestScheduler();
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, scheduler).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // when
        // Do not advance the scheduler so that nothing happens

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(0);
        results.Messages.Count.Should().Be(0);
        results.Summary.Overall.Adds.Should().Be(0);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EnumObservableUsesTheSchedulerAndEmitsAll()
    {
        // having
        var scheduler = new TestScheduler();
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, scheduler).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // when
        scheduler.AdvanceBy(MarketCount);

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = Add(new Market(0));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // then
        _marketList.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EqualityComparerAndComparerWorkTogetherForUpdates()
    {
        // having
        var market1 = Add(new Market(0));
        var market2 = Add(new Market(1));

        var results = market1.LatestPrices.MergeChangeSets(market2.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LatestPriceCompare).AsAggregator();
        var resultsTimeStamp = market1.LatestPrices.MergeChangeSets(market2.LatestPrices, MarketPrice.EqualityComparerWithTimeStamp, MarketPrice.LatestPriceCompare).AsAggregator();
        market1.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market2.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        market2.SetPrices(0, PricesPerMarket, LowestPrice);

        // then
        _marketList.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Messages.Count.Should().Be(2);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        resultsTimeStamp.Messages.Count.Should().Be(3);
        resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
        resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EqualityComparerAndComparerWorkTogetherForRefreshes()
    {
        // having
        var market1 = Add(new Market(0));
        var market2 = Add(new Market(1));

        var results1 = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, MarketPrice.LatestPriceCompare).AsAggregator();
        var results2 = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparerWithTimeStamp, MarketPrice.LatestPriceCompare).AsAggregator();
        market1.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market2.SetPrices(0, PricesPerMarket, LowestPrice);
        // Update again, but only the timestamp will change, so results1 will ignore
        market2.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        // results1 won't see the refresh because it ignored the update
        // results2 will see the refreshes because it didn't
        market2.RefreshAllPrices(LowestPrice);

        // then
        _marketList.Count.Should().Be(2);
        results1.Data.Count.Should().Be(PricesPerMarket);
        results1.Messages.Count.Should().Be(2);
        results1.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results1.Summary.Overall.Removes.Should().Be(0);
        results1.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results1.Summary.Overall.Refreshes.Should().Be(0);
        results2.Messages.Count.Should().Be(4);
        results2.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results2.Summary.Overall.Removes.Should().Be(0);
        results2.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results2.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void EqualityComparerAndComparerRefreshesBecomeUpdates()
    {
        // having
        var market1 = Add(new Market(0));
        var market2 = Add(new Market(1));

        var results1 = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer, MarketPrice.LatestPriceCompare).AsAggregator();
        var results2 = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparerWithTimeStamp, MarketPrice.LatestPriceCompare).AsAggregator();
        market1.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market2.SetPrices(0, PricesPerMarket, LowestPrice - 1);
        // Update again, but only the timestamp will change, so results1 will ignore
        market2.SetPrices(0, PricesPerMarket, LowestPrice - 1);

        // when
        // results1 will see this as an update because it ignored the last update
        // results2 will see the refreshes
        market2.RefreshAllPrices(GetRandomPrice);

        // then
        _marketList.Count.Should().Be(2);
        results1.Data.Count.Should().Be(PricesPerMarket);
        results1.Messages.Count.Should().Be(3);
        results1.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results1.Summary.Overall.Removes.Should().Be(0);
        results1.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results1.Summary.Overall.Refreshes.Should().Be(0);
        results2.Messages.Count.Should().Be(4);
        results2.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results2.Summary.Overall.Removes.Should().Be(0);
        results2.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results2.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        results.IsCompleted.Should().Be(true);
        results.Data.Count.Should().Be(PricesPerMarket * MarketCount);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * MarketCount);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MergedObservableCompletesWhenAllSourcesComplete(bool completeSources)
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeSources)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        results.IsCompleted.Should().Be(completeSources);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MergedObservableRespectsCompletableFlag(bool completeSource, bool completeChildren)
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: completeSource).AsAggregator();

        // then
        results.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void ObservableObservableContainsAllAddedValues()
    {
        // having
        var scheduler = new TestScheduler();
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        var marketObs = Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Select(n => _marketList[(int)n]);
        using var results = marketObs.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        Enumerable.Range(0, MarketCount).ForEach(n => scheduler.AdvanceBy(Interval.Ticks));

        // when
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // then
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void ObservableObservableContainsAllExistingValues()
    {
        // having
        var scheduler = new TestScheduler();
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        var marketObs = Observable.Interval(Interval, scheduler).Select(n => _marketList[(int)n]);
        using var results = marketObs.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();

        // when
        Enumerable.Range(0, MarketCount).ForEach(n => scheduler.AdvanceBy(Interval.Ticks));

        // then
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void MergedObservableWillFailIfAnyChangeChangeSetFails()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        var expectedError = new Exception("Test exception");
        var enumObservable = _marketList.Select(m => m.LatestPrices).Append(Observable.Throw<IChangeSet<MarketPrice, int>>(expectedError));

        // when
        using var results = enumObservable.MergeChangeSets().AsAggregator();

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ObservableObservableWillFailIfSourceFails()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        var expectedError = new Exception("Test exception");
        var observables = _marketList.Select(m => m.LatestPrices).ToObservable().Concat(Observable.Throw<IObservable<IChangeSet<MarketPrice, int>>>(expectedError));

        // when
        using var results = observables.MergeChangeSets().AsAggregator();

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ObservableObservableCompletesIfAndOnlyIfSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        // having
        var fixedMarkets = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren));
        var observableObservable = fixedMarkets.Select(m => m.LatestPrices).ToObservable();
        if (!completeSource)
        {
            observableObservable = observableObservable.Concat(Observable.Never<IObservable<IChangeSet<MarketPrice, int>>>());
        }

        // when
        using var results = observableObservable.MergeChangeSets().AsAggregator();

        // then
        results.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    public void Dispose() => _marketList.ForEach(m => (m as IDisposable)?.Dispose());

    private Market Add(Market addThis)
    {
        _marketList.Add(addThis);
        return addThis;
    }
}
