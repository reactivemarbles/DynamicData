using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class MergeManyChangeSetsCacheFixture : IDisposable
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

    private static readonly TimeSpan s_MaxAddTime = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan s_MaxRemoveTime = TimeSpan.FromSeconds(2.0);

    private readonly ISourceCache<IMarket, Guid> _marketCache = new SourceCache<IMarket, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<IMarket, Guid> _marketCacheResults;

    private readonly Randomizer _randomizer = new (0x21123737);

    private int _uniquePriceId;

    public MergeManyChangeSetsCacheFixture() => _marketCacheResults = _marketCache.Connect().AsAggregator();

    [Theory]
    [InlineData(5, 7)]
    [InlineData(10, 50)]
    [InlineData(10, 1_000)]
    [InlineData(200, 500)]
    [InlineData(1_000, 10)]
    public async Task MultiThreadedStressTest(int marketCount, int priceCount)
    {
        using var priceResults = _marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices).AsAggregator();

        _ = await AddRemoveStress(marketCount, priceCount, TaskPoolScheduler.Default)
            .Finally(() => CheckResultContents(_marketCacheResults, priceResults));
    }

    [Theory]
    [InlineData(5, 7)]
    [InlineData(5, 200)]
    [InlineData(10, 100)]
    [InlineData(20, 50)]
    [InlineData(100, 10)]
    public void NoDeadlockOrExceptionIfSubscribeDuringModify(int marketCount, int priceCount)
    {
        // Arrange
        Func<Task> CreateTest(IScheduler sch, int markets, int prices) =>
            async () =>
            {
                var merged = _marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices);

                var adding = true;

                using var addMarkets = GenerateMarkets(sch)
                    .Take(markets)
                    .StressAddRemove(_marketCache, _ => GetRemoveTime(), sch)
                    .Finally(() => _marketCache.Dispose())
                    .Subscribe();

                using var addPrices = _marketCache.Connect()
                    .MergeMany(market => AddRemove((Market)market, sch, prices))
                    .Finally(() => adding = false)
                    .Subscribe();

                do
                {
                    // Ensure items are being added asynchronously before subscribing to the animal changes
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
            };

        // Act

        // Assert
        CreateTest(TaskPoolScheduler.Default, marketCount, priceCount).Should().NotThrowAsync();
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
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

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
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

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
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

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
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

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
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

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
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketCache.AddOrUpdate(market);
        var updatedMarket = new Market(market);
        updatedMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

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

    [Fact]
    public void ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // when
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowLow = new Market(marketLow);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketLowLow.SetPrices(0, PricesPerMarket, LowestPrice - 1);
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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
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
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
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
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
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
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
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
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(market);

        // when
        market.SetPrices(0, PricesPerMarket, LowestPrice);

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
    public void EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices).AsAggregator();
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
        _marketCache.AddOrUpdate(markets);
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<IMarket, Guid>>(expectedError);

        using var cleanup = _marketCache.Connect().Concat(throwObservable)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        receivedError.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        DisposeMarkets();
    }

    private IObservable<Unit> AddRemoveStress(int marketCount, int priceCount, IScheduler scheduler) =>
        Observable.Create<Unit>(observer => new CompositeDisposable
            {
                GenerateMarkets(scheduler)
                        .Take(marketCount)
                        .StressAddRemove(_marketCache, _ => GetRemoveTime(), scheduler)
                        .Finally(() => _marketCache.Dispose())
                        .Subscribe(
                            onNext: _ => { },
                            onError: ex => observer.OnError(ex)),

                _marketCache.Connect()
                        .MergeMany(market => AddRemove((Market)market, scheduler, priceCount))
                        .Subscribe(
                            onNext: _ => { },
                            onError: ex => observer.OnError(ex),
                            onCompleted: () =>
                                {
                                    observer.OnNext(Unit.Default);
                                    observer.OnCompleted();
                                })
            });

    private IObservable<MarketPrice> AddRemove(Market market, IScheduler sch, int addCount) =>
        GeneratePrices(market, sch)
            .Take(addCount)
            .StressAddRemove(market.PricesCache, _ => GetRemoveTime(), sch)
            .Finally(market.PricesCache.Dispose);

    private IObservable<IMarket> GenerateMarkets(IScheduler scheduler) =>
        _randomizer.Interval(s_MaxAddTime, scheduler).Select(_ => new Market(_randomizer.Utf16String(5, 10, true)));

    private IObservable<MarketPrice> GeneratePrices(Market market, IScheduler scheduler) =>
        _randomizer.Interval(s_MaxAddTime, scheduler).Select(_ => market.CreatePrice(Interlocked.Increment(ref _uniquePriceId), GetRandomPrice()));

    private void CheckResultContents(ChangeSetAggregator<IMarket, Guid> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults)
    {
        var expectedMarkets = _marketCache.Items.ToList();
        var expectedPrices = expectedMarkets.SelectMany(market => ((Market)market).PricesCache.Items).ToList();

        // These should be subsets of each other
        expectedMarkets.Should().BeSubsetOf(marketResults.Data.Items);
        marketResults.Data.Items.Should().BeSubsetOf(expectedMarkets);
        marketResults.Data.Items.Count().Should().Be(expectedMarkets.Count);

        // These should be subsets of each other
        expectedPrices.Should().BeSubsetOf(priceResults.Data.Items);
        priceResults.Data.Items.Should().BeSubsetOf(expectedPrices);
        priceResults.Data.Items.Count().Should().Be(expectedPrices.Count);
    }

    private void DisposeMarkets()
    {
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
        _marketCache.Clear();
    }

    private decimal GetRandomPrice() => MarketPrice.RandomPrice(_randomizer, BasePrice, PriceOffset);

    private TimeSpan? GetRemoveTime() => _randomizer.Bool() ? NextRemoveTime() : null;
    private TimeSpan NextRemoveTime() => _randomizer.TimeSpan(s_MaxRemoveTime);
}
