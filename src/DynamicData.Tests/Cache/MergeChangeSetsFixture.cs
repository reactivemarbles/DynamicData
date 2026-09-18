#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

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

    private readonly List<Market> _marketList = new();
    private readonly Random _random = new(0x12291977);

    private static decimal GetDeterministicPrice(int marketIndex) => BasePrice + marketIndex;

    private decimal GetRandomPrice() => MarketPrice.RandomPrice(_random, BasePrice, PriceOffset);

    public MergeChangeSetsFixture()
    {
    }

    [Test]
    public async Task NullChecks()
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
        await Assert.That(emptyChangeSetObs).IsNotNull();
        await Assert.That(emptyChangeSetObsObs).IsNotNull();
        await Assert.That(emptyChangeSetObsEnum).IsNotNull();
        await Assert.That(comparer).IsNotNull();
        await Assert.That(equalityComparer).IsNotNull();
        await Assert.That(nullChangeSetObs).IsNull();
        await Assert.That(nullChangeSetObsObs).IsNull();
        await Assert.That(nullComparer).IsNull();
        await Assert.That(nullEqualityComparer).IsNull();
        await Assert.That(nullChangeSetObsEnum).IsNull();

        await Assert.That(obsobs).Throws<ArgumentNullException>();
        await Assert.That(obsobsComp).Throws<ArgumentNullException>();
        await Assert.That(obsobsComp1).Throws<ArgumentNullException>();
        await Assert.That(obsobsEq).Throws<ArgumentNullException>();
        await Assert.That(obsobsEq1).Throws<ArgumentNullException>();
        await Assert.That(obsobsEqComp).Throws<ArgumentNullException>();
        await Assert.That(obsobsEqComp1).Throws<ArgumentNullException>();
        await Assert.That(obsobsEqComp2).Throws<ArgumentNullException>();
        await Assert.That(obspair).Throws<ArgumentNullException>();
        await Assert.That(obspairB).Throws<ArgumentNullException>();
        await Assert.That(obspairComp).Throws<ArgumentNullException>();
        await Assert.That(obspairCompB).Throws<ArgumentNullException>();
        await Assert.That(obspairComp1).Throws<ArgumentNullException>();
        await Assert.That(obspairEq).Throws<ArgumentNullException>();
        await Assert.That(obspairEqB).Throws<ArgumentNullException>();
        await Assert.That(obspairEq1).Throws<ArgumentNullException>();
        await Assert.That(obspairEqComp).Throws<ArgumentNullException>();
        await Assert.That(obspairEqCompB).Throws<ArgumentNullException>();
        await Assert.That(obspairEqComp1).Throws<ArgumentNullException>();
        await Assert.That(obspairEqComp2).Throws<ArgumentNullException>();
        await Assert.That(obsEnum).Throws<ArgumentNullException>();
        await Assert.That(obsEnumB).Throws<ArgumentNullException>();
        await Assert.That(obsEnumComp).Throws<ArgumentNullException>();
        await Assert.That(obsEnumCompB).Throws<ArgumentNullException>();
        await Assert.That(obsEnumComp1).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEq).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEqB).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEq1).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEqComp).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEqCompB).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEqComp1).Throws<ArgumentNullException>();
        await Assert.That(obsEnumEqComp2).Throws<ArgumentNullException>();
        await Assert.That(enumObs).Throws<ArgumentNullException>();
        await Assert.That(enumObsComp).Throws<ArgumentNullException>();
        await Assert.That(enumObsComp1).Throws<ArgumentNullException>();
        await Assert.That(enumObsEq).Throws<ArgumentNullException>();
        await Assert.That(enumObsEq1).Throws<ArgumentNullException>();
        await Assert.That(enumObsEqComp).Throws<ArgumentNullException>();
        await Assert.That(enumObsEqComp1).Throws<ArgumentNullException>();
        await Assert.That(enumObsEqComp2).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AllExistingItemsPresentInResult()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AllNewSubItemsPresentInResult()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var pricesCache = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsObservableCache();
        using var results = pricesCache.Connect().AsAggregator();

        // when
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AllRefreshedSubItemsAreRefreshed()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(MarketCount * PricesPerMarket);
    }

    [Test]
    public async Task AnyDuplicateKeyValuesShouldBeHidden()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();

        // when
        _marketList[0].SetPrices(0, PricesPerMarket, GetDeterministicPrice(0));
        _marketList[1].SetPrices(0, PricesPerMarket, GetDeterministicPrice(1));

        // then
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(_marketList[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetDeterministicPrice(0));
        _marketList[1].SetPrices(0, PricesPerMarket, GetDeterministicPrice(1));

        // when
        _marketList[1].RemoveAllPrices();

        // then
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(_marketList[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetDeterministicPrice(0));
        _marketList[1].SetPrices(0, PricesPerMarket, GetDeterministicPrice(1));

        // when
        _marketList[0].RemoveAllPrices();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(_marketList[1].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, 2).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList[0].SetPrices(0, PricesPerMarket, GetDeterministicPrice(0));
        _marketList[1].SetPrices(0, PricesPerMarket, GetDeterministicPrice(1));

        // when
        _marketList[1].RefreshAllPrices(GetDeterministicPrice(2));

        // then
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var pair in results.Data.Items.Zip(_marketList[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
    }

    [Test]
    public async Task AnyRemovedSubItemIsRemoved()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount).ToList())));

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * (PricesPerMarket - RemoveCount));
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(MarketCount * RemoveCount);
    }

    [Test]
    public async Task ComparerOnlyAddsBetterAddedValues()
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
        await Assert.That(_marketList.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerOnlyAddsBetterExistingValues()
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
        await Assert.That(_marketList.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRefresh()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketFlipFlop.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRemove()
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
        await Assert.That(_marketList.Count).IsEqualTo(3);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnUpdate()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketFlipFlop.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerOnlyUpdatesVisibleValuesOnUpdate()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerOnlyRefreshesVisibleValues()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task EnumObservableUsesTheScheduler()
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
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Messages.Count).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EnumObservableUsesTheSchedulerAndEmitsAll()
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
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = Add(new Market(0));
        using var results = _marketList.Select(m => m.LatestPrices).MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // then
        await Assert.That(_marketList.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EqualityComparerAndComparerWorkTogetherForUpdates()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Messages.Count).IsEqualTo(3);
        await Assert.That(resultsTimeStamp.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsTimeStamp.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(resultsTimeStamp.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EqualityComparerAndComparerWorkTogetherForRefreshes()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results1.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results1.Messages.Count).IsEqualTo(3);
        await Assert.That(results1.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results1.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results1.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results1.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        await Assert.That(results2.Messages.Count).IsEqualTo(4);
        await Assert.That(results2.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results2.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results2.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results2.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task EqualityComparerAndComparerRefreshesBecomeUpdates()
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
        await Assert.That(_marketList.Count).IsEqualTo(2);
        await Assert.That(results1.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results1.Messages.Count).IsEqualTo(3);
        await Assert.That(results1.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results1.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results1.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results1.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(results2.Messages.Count).IsEqualTo(4);
        await Assert.That(results2.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results2.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results2.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results2.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        await Assert.That(results.IsCompleted).IsTrue();
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * MarketCount);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MergedObservableCompletesWhenAllSourcesComplete(bool completeSources)
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeSources)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: true).AsAggregator();

        // then
        await Assert.That(results.IsCompleted).IsEqualTo(completeSources);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MergedObservableRespectsCompletableFlag(bool completeSource, bool completeChildren)
    {
        // having
        var fixedMarketList = Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)).ToList();

        // when
        using var results = fixedMarketList.Select(m => m.LatestPrices).MergeChangeSets(completable: completeSource).AsAggregator();

        // then
        await Assert.That(results.IsCompleted).IsEqualTo(completeSource && completeChildren);
    }

    [Test]
    public async Task ObservableObservableContainsAllAddedValues()
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
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task ObservableObservableContainsAllExistingValues()
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
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task MergedObservableWillFailIfAnyChangeChangeSetFails()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        var expectedError = new Exception("Test exception");
        var enumObservable = _marketList.Select(m => m.LatestPrices).Append(Observable.Throw<IChangeSet<MarketPrice, int>>(expectedError));

        // when
        using var results = enumObservable.MergeChangeSets().AsAggregator();

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ObservableObservableWillFailIfSourceFails()
    {
        // having
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new Market(n)));
        _marketList.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));
        var expectedError = new Exception("Test exception");
        var observables = _marketList.Select(m => m.LatestPrices).ToObservable().Concat(Observable.Throw<IObservable<IChangeSet<MarketPrice, int>>>(expectedError));

        // when
        using var results = observables.MergeChangeSets().AsAggregator();

        // then
        await Assert.That(_marketList.Count).IsEqualTo(MarketCount);
        await Assert.That(_marketList.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ObservableObservableCompletesIfAndOnlyIfSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
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
        await Assert.That(results.IsCompleted).IsEqualTo(completeSource && completeChildren);
    }

    public void Dispose() => _marketList.ForEach(m => (m as IDisposable)?.Dispose());

    private Market Add(Market addThis)
    {
        _marketList.Add(addThis);
        return addThis;
    }
}
