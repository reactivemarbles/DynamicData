using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Bogus;

namespace DynamicData.Tests.Domain;

internal sealed class MarketPrice
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

    public static decimal RandomPrice(Random r, decimal basePrice, decimal offset) => basePrice + (decimal)r.NextDouble() * offset;

    public static decimal RandomPrice(Randomizer r, decimal basePrice, decimal offset) => r.Decimal(basePrice, basePrice + offset);

    public override bool Equals(object? obj) => obj is MarketPrice price && Price == price.Price && TimeStamp.Equals(price.TimeStamp) && MarketId.Equals(price.MarketId) && ItemId == price.ItemId;

    public override int GetHashCode() => HashCode.Combine(Price, TimeStamp, MarketId, ItemId);

    public static bool operator ==(MarketPrice? left, MarketPrice? right) => EqualityComparer<MarketPrice>.Default.Equals(left, right);

    public static bool operator !=(MarketPrice? left, MarketPrice? right) => !(left == right);

    private class CurrentPriceEqualityComparer : IEqualityComparer<MarketPrice>
    {
        public virtual bool Equals([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y) => x.MarketId.Equals(x.MarketId) && x.ItemId == y.ItemId && x.Price == y.Price;
        public int GetHashCode([DisallowNull] MarketPrice obj) => throw new NotImplementedException();
    }

    private sealed class TimeStampPriceEqualityComparer : CurrentPriceEqualityComparer, IEqualityComparer<MarketPrice>
    {
        public override bool Equals([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y) => base.Equals(x, y) && x.TimeStamp == y.TimeStamp;
    }

    private sealed class LowestPriceComparer : IComparer<MarketPrice>
    {
        public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return x.Price.CompareTo(y.Price);
        }
    }

    private sealed class HighestPriceComparer : IComparer<MarketPrice>
    {
        public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return y.Price.CompareTo(x.Price);
        }
    }

    private sealed class LatestPriceComparer : IComparer<MarketPrice>
    {
        public int Compare([DisallowNull] MarketPrice x, [DisallowNull] MarketPrice y)
        {
            Debug.Assert(x.ItemId == y.ItemId);
            return y.TimeStamp.CompareTo(x.TimeStamp);
        }
    }
}
