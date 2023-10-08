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
    private static readonly IEqualityComparer<CurrentPrice> EqualityComparer = new CurrentPriceEqualityComparer();
    private static readonly IComparer<CurrentPrice> HighPriceCompare = new HighestPriceComparer();
    private static readonly IComparer<CurrentPrice> LowPriceCompare = new LowestPriceComparer();
    private static readonly IComparer<CurrentPrice> LatestPriceCompare = new LatestPriceComparer();
    private static readonly Random Random = new Random(0x21123737);

    private readonly ISourceCache<Market, Guid> _marketCache = new SourceCache<Market, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<Market, Guid> _marketCacheResults;

    private readonly ChangeSetAggregator<CurrentPrice, int> _lowPriceResults;

    private readonly ChangeSetAggregator<CurrentPrice, int> _highPriceResults;

    private readonly ChangeSetAggregator<CurrentPrice, int> _latestPriceResults;

    private readonly ChangeSetAggregator<CurrentPrice, int> _unsortedResults;

    public MergeManyChangeSetsFixture()
    {
        _marketCacheResults = _marketCache.Connect().AsAggregator();
        _highPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, HighPriceCompare).AsAggregator();
        _lowPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, LowPriceCompare).AsAggregator();
        _latestPriceResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer, LatestPriceCompare).AsAggregator();
        _unsortedResults = _marketCache.Connect().MergeMany(m => m.LatestPrices.Connect(), EqualityComparer).AsAggregator();
    }

    [Fact]
    public void FactoryIsInvoked()
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
    public void FactoryWithKeyIsInvoked()
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

    public void Dispose()
    {
        _marketCache.Dispose();
        _marketCacheResults.Dispose();
        _lowPriceResults.Dispose();
        _highPriceResults.Dispose();
        _latestPriceResults.Dispose();
        _unsortedResults.Dispose();
    }

    private class Market
    {
        public Market(int name)
        {
            Name = $"Market #{name}";
        }

        public string Name { get; }

        public Guid Id { get; } = Guid.NewGuid();

        public ISourceCache<CurrentPrice, int> LatestPrices { get; } = new SourceCache<CurrentPrice, int>(p => p.ItemId);

        public void AddOrUpdatePrice(int itemId, decimal price)
        {
            LatestPrices.AddOrUpdate(new CurrentPrice(itemId, price, Id));
        }

        public void AddRandomPrices(Random r, int count, int minId, int maxId) =>
            Enumerable.Range(0, count).Select(_ => r.Next(minId, maxId)).Select(id => new CurrentPrice(id, 10.0m + ((decimal)r.NextDouble() * 10.0m), Id)).ForEach(LatestPrices.AddOrUpdate);

        public void AddRandomPrices(Random r, int minId, int maxId) =>
            Enumerable.Range(minId, (maxId - minId)).Select(id => new CurrentPrice(id, 10.0m + ((decimal)r.NextDouble() * 10.0m), Id)).ForEach(LatestPrices.AddOrUpdate);
    }

    private class CurrentPrice : AbstractNotifyPropertyChanged
    {
        private decimal _price;
        private DateTimeOffset _timeStamp;

        public CurrentPrice(int itemId, decimal price, Guid marketId)
        {
            ItemId = itemId;
            MarketId = marketId;
            Price = price;
        }

        public decimal Price
        {
            get => _price;
            set => SetAndRaise(ref _price, value);
        }

        public DateTimeOffset TimeStamp => _timeStamp;

        public Guid MarketId { get; }

        public int ItemId { get; }

        protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName != nameof(TimeStamp))
            {
                SetAndRaise(ref _timeStamp, DateTimeOffset.UtcNow);
            }
        }
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
            Debug.Assert(x.MarketId == y.MarketId);
            Debug.Assert(x.ItemId == y.ItemId);
            return x.Price.CompareTo(y.Price);
        }
    }

    private class LowestPriceComparer : IComparer<CurrentPrice>
    {
        public int Compare([DisallowNull] CurrentPrice x, [DisallowNull] CurrentPrice y)
        {
            Debug.Assert(x.MarketId == y.MarketId);
            Debug.Assert(x.ItemId == y.ItemId);
            return y.Price.CompareTo(x.Price);
        }
    }

    private class LatestPriceComparer : IComparer<CurrentPrice>
    {
        public int Compare([DisallowNull] CurrentPrice x, [DisallowNull] CurrentPrice y)
        {
            Debug.Assert(x.MarketId == y.MarketId);
            Debug.Assert(x.ItemId == y.ItemId);
            return x.TimeStamp.CompareTo(y.TimeStamp);
        }
    }
}
