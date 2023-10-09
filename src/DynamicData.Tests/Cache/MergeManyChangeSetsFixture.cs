using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class MergeManyChangeSetsFixture : IDisposable
{
    const int MarketCount = 10;
    const int PricesPerMarket = 10;
    const int RemoveCount = 5;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;

    private static readonly IEqualityComparer<CurrentPrice> EqualityComparer = new CurrentPriceEqualityComparer();
    private static readonly IComparer<CurrentPrice> HighPriceCompare = new HighestPriceComparer();
    private static readonly IComparer<CurrentPrice> LowPriceCompare = new LowestPriceComparer();
    private static readonly IComparer<CurrentPrice> LatestPriceCompare = new LatestPriceComparer();
    private static readonly Random Random = new Random(0x21123737);

    private readonly ISourceCache<Market, Guid> _marketCache = new SourceCache<Market, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<Market, Guid> _marketCacheResults;

    //private readonly ChangeSetAggregator<CurrentPrice, int> _lowPriceResults;

    //private readonly ChangeSetAggregator<CurrentPrice, int> _highPriceResults;

    //private readonly ChangeSetAggregator<CurrentPrice, int> _latestPriceResults;

    //private readonly ChangeSetAggregator<CurrentPrice, int> _unsortedResults;

    //private readonly ChangeSetAggregator<CurrentPrice, int> _baseResults;

    public MergeManyChangeSetsFixture()
    {
        _marketCacheResults = _marketCache.Connect().AsAggregator();
        //_highPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, HighPriceCompare).AsAggregator();
        //_lowPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, LowPriceCompare).AsAggregator();
        //_latestPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, LatestPriceCompare).AsAggregator();
        //_unsortedResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer).AsAggregator();
        //_baseResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();
    }

    [Fact]
    public void AbleToInvokeFactory()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<CurrentPrice, int>> factory(Market m)
        {
            invoked = true;
            return m.LatestPrices.Connect();
        }
        using var sub = _marketCache.Connect().MergeMany(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, IObservable<IChangeSet<CurrentPrice, int>>>)null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, IObservable<IChangeSet<CurrentPrice, int>>>)null!, equalityComparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, IObservable<IChangeSet<CurrentPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, IObservable<IChangeSet<CurrentPrice, int>>>)null!, null, null));
    }

    [Fact]
    public void AbleToInvokeFactoryWithKey()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<CurrentPrice, int>> factory(Market m, Guid g)
        {
            invoked = true;
            return m.LatestPrices.Connect();
        }
        using var sub = _marketCache.Connect().MergeMany(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, equalityComparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => _marketCache.Connect().MergeMany((Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, null, null));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeMany(null!, (Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeMany(null!, (Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, equalityComparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeMany(null!, (Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, comparer: null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.MergeMany(null!, (Func<Market, Guid, IObservable<IChangeSet<CurrentPrice, int>>>)null!, null, null));
    }

    [Fact]
    public void AllSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();

        // when
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * 1000, (m.Index * 1000) + PricesPerMarket));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.LatestPrices.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount*PricesPerMarket);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeHiddenFromResultWithoutComparer()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // then
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].LatestPrices.Items).ToList().ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeUnhiddenFromResultWithoutComparer()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].AddRandomPrices(Random, 0, PricesPerMarket);
        markets[1].AddRandomPrices(Random, 0, PricesPerMarket);

        // when
        _marketCache.Remove(markets[0]);

        // then
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[1].LatestPrices.Items).ToList().ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Updates.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void AnyRemovedSubItemIsRemovedFromResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * 1000, (m.Index * 1000) + PricesPerMarket));

        // when
        markets.ForEach(m => m.LatestPrices.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
        results.Messages.Count.Should().Be(MarketCount * 2);
        results.Messages[0].Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
    }

    [Fact]
    public void AnySourceItemRemovedRemovesAllSourceValuesFromResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect()).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.AddRandomPrices(Random, m.Index * 1000, (m.Index * 1000) + PricesPerMarket));

        // when
        _marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount - RemoveCount);
        results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
    }

    [Fact]
    public void ComparerOnlyAddsBetterValuesToResult()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // when
        marketLow.SetFixedPrice(0, PricesPerMarket, BasePrice - 1.0m);
        marketHigh.SetFixedPrice(0, PricesPerMarket, BasePrice + PriceOffset + 1.0m);

        // then
        highPriceResults.Data.Count.Should().Be(PricesPerMarket);
        highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        highPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        highPriceResults.Data.Items.Select(cp => cp.MarketId).ToList().ForEach(guid => guid.Should().Be(marketHigh.Id));
        lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        lowPriceResults.Data.Items.Select(cp => cp.MarketId).ToList().ForEach(guid => guid.Should().Be(marketLow.Id));
    }

    [Fact]
    public void ComparerUpdatesToCorrectValueOnRemove()
    {
        // having
        using var lowPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.AddRandomPrices(Random, 0, PricesPerMarket);
        marketLow.SetFixedPrice(0, PricesPerMarket, BasePrice - 1.0m);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        _marketCache.Remove(marketLow);

        // then
        highPriceResults.Data.Count.Should().Be(PricesPerMarket);
        highPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        highPriceResults.Summary.Overall.Updates.Should().Be(0);
        highPriceResults.Data.Items.Select(cp => cp.MarketId).ToList().ForEach(guid => guid.Should().Be(marketOriginal.Id));
        lowPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket*2);
        lowPriceResults.Data.Items.Select(cp => cp.MarketId).ToList().ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

    [Fact]
    public void UpdatingSourceRemovesPreviousAndAddsNewValuesToResult()
    {
        // having

        // when

        // then
    }

    [Fact]
    public void UpdatingValueInResultUpdatesResultCorrectly()
    {
        // having

        // when

        // then
    }

    [Fact]
    public void UpdatingValueNotInResultUpdatesResultCorrectly()
    {
        // having

        // when

        // then
    }

    [Fact]
    public void RefreshingValueInResultUpdatesResultCorrectly()
    {
        // having

        // when

        // then
    }

    [Fact]
    public void RefreshingValueNotInResultUpdatesResultCorrectly()
    {
        // having

        // when

        // then
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        //_lowPriceResults.Dispose();
        //_highPriceResults.Dispose();
        //_latestPriceResults.Dispose();
        //_unsortedResults.Dispose();
        //_baseResults.Dispose();
        _marketCache.Items.ForEach(m => m.Dispose());
        _marketCache.Dispose();
    }

    private class Market : IDisposable
    {
        public Market(int name, Guid id)
        {
            Name = $"Market #{name}";
            Id = id;
        }

        public Market(int name) : this(name, Guid.NewGuid())
        {

        }

        public string Name { get; }

        public Guid Id { get; }

        public ISourceCache<CurrentPrice, int> LatestPrices { get; } = new SourceCache<CurrentPrice, int>(p => p.ItemId);

        public void SetPrice(int itemId, decimal price) => LatestPrices.AddOrUpdate(new CurrentPrice(itemId, price, Id));

        public void RefreshPrice(int id, decimal newPrice) =>
            LatestPrices.Lookup(id).IfHasValue(cp =>
            {
                cp.Price = newPrice;
                LatestPrices.Refresh(cp);
            });

        public void SetAllPrices(decimal newPrice) =>
            LatestPrices.Edit(updater => updater.Items.Select(cp => new CurrentPrice(cp.ItemId, newPrice, Id)));

        public void RefreshAllPrices(decimal newPrice) =>
            LatestPrices.Edit(updater => updater.Items.ForEach(cp =>
            {
                cp.Price = newPrice;
                updater.Refresh(cp);
            }));

        public void AddRandomPrices(Random r, int count, int minId, int maxId) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => new CurrentPrice(id, RandomPrice(r), Id)));

        public void AddRandomPrices(Random r, int minId, int maxId) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => new CurrentPrice(id, RandomPrice(r), Id)));

        public void RemovePrice(int itemId) => LatestPrices.Remove(itemId);

        public void SetFixedPrice(int minId, int maxId, decimal newPrice) =>
            LatestPrices.AddOrUpdate(Enumerable.Range(minId, (maxId - minId)).Select(id => new CurrentPrice(id, newPrice, Id)));

        public void Dispose() => LatestPrices.Dispose();

        private static decimal RandomPrice(Random r) => BasePrice + ((decimal)r.NextDouble() * PriceOffset);
    }

    private class CurrentPrice
    {
        private decimal _price;

        public CurrentPrice(int itemId, decimal price, Guid marketId)
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
    }

    private class CurrentPriceEqualityComparer : IEqualityComparer<CurrentPrice>
    {
        public bool Equals(CurrentPrice? x, CurrentPrice? y) => x.MarketId.Equals(x.MarketId) && (x?.ItemId == y?.ItemId) && (x?.Price == y?.Price) && (x?.TimeStamp == y?.TimeStamp);
        public int GetHashCode([DisallowNull] CurrentPrice obj) => throw new NotImplementedException();
    }

    private class HighestPriceComparer : IComparer<CurrentPrice>
    {
        public int Compare([DisallowNull] CurrentPrice x, [DisallowNull] CurrentPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return x.Price.CompareTo(y.Price);
        }
    }

    private class LowestPriceComparer : IComparer<CurrentPrice>
    {
        public int Compare([DisallowNull] CurrentPrice x, [DisallowNull] CurrentPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return y.Price.CompareTo(x.Price);
        }
    }

    private class LatestPriceComparer : IComparer<CurrentPrice>
    {
        public int Compare([DisallowNull] CurrentPrice x, [DisallowNull] CurrentPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return x.TimeStamp.CompareTo(y.TimeStamp);
        }
    }
}
