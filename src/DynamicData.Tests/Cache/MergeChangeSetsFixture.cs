using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;
using Xunit.Sdk;

namespace DynamicData.Tests.Cache;

public sealed class MergeChangeSetsFixture : IDisposable
{
    //const int MarketCount = 101;
    //const int PricesPerMarket = 103;
    //const int RemoveCount = 53;
    const int MarketCount = 3;
    const int PricesPerMarket = 7;
    const int RemoveCount = 5;
    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    private static readonly Random Random = new Random(0x12291977);

    private readonly List<Market> _marketList = new ();

    public MergeChangeSetsFixture()
    {
    }

    [Fact]
    public void NullChecks()
    {
        // having
        var neverObservable = Observable.Never<Optional<int>>();
        var nullObservable = (IObservable<Optional<int>>)null!;
        var nullConverter = (Func<int, double>)null!;
        var nullOptionalConverter = (Func<int, Optional<double>>)null!;
        var converter = (Func<int, double>)(i => i);
        var nullFallback = (Func<int>)null!;
        var nullConvertFallback = (Func<double>)null!;
        var nullOptionalFallback = (Func<Optional<int>>)null!;
        var action = (Action)null!;
        var actionVal = (Action<int>)null!;
        var nullExceptionGenerator = (Func<Exception>)null!;

        // when
        var convert1 = () => nullObservable.Convert(nullConverter);
        var convert2 = () => neverObservable.Convert(nullConverter);
        var convertOpt1 = () => nullObservable.Convert(nullOptionalConverter);
        var convertOpt2 = () => neverObservable.Convert(nullOptionalConverter);
        var convertOr1 = () => nullObservable.ConvertOr(nullConverter, nullConvertFallback);
        var convertOr2 = () => neverObservable.ConvertOr(nullConverter, nullConvertFallback);
        var convertOr3 = () => neverObservable.ConvertOr(converter, nullConvertFallback);
        var orElse1 = () => nullObservable.OrElse(nullOptionalFallback);
        var orElse2 = () => neverObservable.OrElse(nullOptionalFallback);
        var onHasValue = () => nullObservable.OnHasValue(actionVal);
        var onHasValue2 = () => neverObservable.OnHasValue(actionVal);
        var onHasNoValue = () => nullObservable.OnHasNoValue(action);
        var onHasNoValue2 = () => neverObservable.OnHasNoValue(action);
        var selectValues = () => nullObservable.SelectValues();
        var valueOr = () => nullObservable.ValueOr(nullFallback);
        var valueOrDefault = () => nullObservable.ValueOrDefault();
        var valueOrThrow1 = () => nullObservable.ValueOrThrow(nullExceptionGenerator);
        var valueOrThrow2 = () => neverObservable.ValueOrThrow(nullExceptionGenerator);

        // then
        convert1.Should().Throw<ArgumentNullException>();
        convert2.Should().Throw<ArgumentNullException>();
        convertOpt1.Should().Throw<ArgumentNullException>();
        convertOpt2.Should().Throw<ArgumentNullException>();
        convertOr1.Should().Throw<ArgumentNullException>();
        convertOr2.Should().Throw<ArgumentNullException>();
        convertOr3.Should().Throw<ArgumentNullException>();
        orElse1.Should().Throw<ArgumentNullException>();
        orElse2.Should().Throw<ArgumentNullException>();
        onHasValue.Should().Throw<ArgumentNullException>();
        onHasValue2.Should().Throw<ArgumentNullException>();
        onHasNoValue.Should().Throw<ArgumentNullException>();
        onHasNoValue2.Should().Throw<ArgumentNullException>();
        selectValues.Should().Throw<ArgumentNullException>();
        valueOr.Should().Throw<ArgumentNullException>();
        valueOrDefault.Should().Throw<ArgumentNullException>();
        valueOrThrow1.Should().Throw<ArgumentNullException>();
        valueOrThrow2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AllExistingItemsPresentInResult()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
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
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // when
        _marketList.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
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
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

        // when
        _marketList.ForEach(m => m.RefreshAllPrices(Random));

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
        _marketList[0].AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList[1].AddRandomPrices(Random, 0, PricesPerMarket);

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
        _marketList[0].AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList[1].AddRandomPrices(Random, 0, PricesPerMarket);

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
        _marketList[0].AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList[1].AddRandomPrices(Random, 0, PricesPerMarket);

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
        _marketList[0].AddRandomPrices(Random, 0, PricesPerMarket);
        _marketList[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        _marketList[1].RefreshAllPrices(Random);

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
        _marketList.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket));

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
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

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
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

        // when
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();

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
        using var highPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.LowPriceCompare).AsAggregator();
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);

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
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);
        marketHigh.UpdatePrices(0, PricesPerMarket, HighestPrice);

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
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketFlipFlop.UpdatePrices(0, PricesPerMarket, HighestPrice);

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
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);

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
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.UpdatePrices(0, PricesPerMarket, LowestPrice);

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
    public void EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = Add(new Market(0));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);

        // when
        market.UpdatePrices(0, PricesPerMarket, LowestPrice);

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
    public void EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        results.Completed.Should().Be(true);
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
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeSources)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        results.Completed.Should().Be(completeSources);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MergedObservableRespectsCompletableFlag(bool completeSource, bool completeChildren)
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(Random, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: completeSource).AsAggregator();

        // then
        results.Completed.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void MergedObservableWillFailIfAnySourceFails()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        var expectedError = new Exception("Test exception");

        var observables = _marketList.SkipLast(1).Select(m => m.LatestPrices)
                                                                        .Concat(_marketList.TakeLast(1).Select(m => m.LatestPrices.ForceFail(expectedError)));
        using var results = observables.MergeChangeSets().AsAggregator();

        // when
        Dispose();

        // then
        _marketList.Count.Should().Be(MarketCount);
        _marketList.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _marketList.ForEach(m => (m as IDisposable)?.Dispose());
    }

    private Market Add(Market addThis)
    {
        _marketList.Add(addThis);
        return addThis;
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

        public MarketPrice CreatePrice(int itemId, decimal price) => new (itemId, price, Id);

        public void AddRandomIdPrices(Random r, int count, int minId, int maxId) =>
            _latestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => CreatePrice(id, RandomPrice(r))));

        public void AddRandomPrices(Random r, int minId, int maxId) =>
            _latestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, RandomPrice(r))));

        public void RefreshAllPrices(decimal newPrice) =>
            _latestPrices.Edit(updater => updater.Items.ForEach(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

        public void RefreshAllPrices(Random r) => RefreshAllPrices(RandomPrice(r));

        public void RefreshPrice(int id, decimal newPrice) =>
            _latestPrices.Edit(updater => updater.Lookup(id).IfHasValue(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

        public void RemoveAllPrices() => _latestPrices.Clear();

        public void RemovePrice(int itemId) => _latestPrices.Remove(itemId);

        public void UpdateAllPrices(decimal newPrice) =>
            _latestPrices.Edit(updater => updater.AddOrUpdate(updater.Items.Select(cp => CreatePrice(cp.ItemId, newPrice))));

        public void UpdatePrices(int minId, int maxId, decimal newPrice) =>
            _latestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => CreatePrice(id, newPrice)));

        public void Dispose() => _latestPrices.Dispose();
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
}

internal static class Extensions
{
    public static T With<T>(this T item, Action<T> action)
    {
        action(item);
        return item;
    }

    public static IObservable<T> ForceFail<T>(this IObservable<T> source, Exception? e) =>
        (e is not null)
            ? source.Concat(Observable.Throw<T>(e))
            : source;
}
