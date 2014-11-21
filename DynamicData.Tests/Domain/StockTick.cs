using System;

namespace DynamicData.Tests.Domain
{

    // Envelope
    public class StockUpdate
    {
        public StockTick Delta { get; set; }
        public StockTick Image { get; set; }

        public StockUpdate Create(StockTick delta)
        {
            return new StockUpdate
            {
                Delta = delta,
                Image = Image == null ? delta : StockTick.Merge(Image, delta)
            };
        }

        public override string ToString()
        {
            return string.Format("\nDELTA: {0}\nIMAGE: {1}", Delta, Image);
        }
    }
    public class StockTick
    {
        public string Symbol { get; set; }

        public decimal? Bid { get; set; }
        public decimal? Ask { get; set; }
        public decimal? Last { get; set; }

        public long? BidSize { get; set; }
        public long? AskSize { get; set; }
        public long? LastSize { get; set; }
        public long? Volume { get; set; }

        public DateTime? QuoteTime { get; set; }
        public DateTime? TradeTime { get; set; }

        public override string ToString()
        {
            return new { Symbol, Bid, Ask, Last, BidSize, AskSize, LastSize, Volume, QuoteTime, TradeTime }.ToString();
        }

        public static StockTick Merge(StockTick a, StockTick b)
        {
            return new StockTick
            {
                Bid = b.Bid ?? a.Bid,
                Ask = b.Ask ?? a.Ask,
                Last = b.Last ?? a.Last,
                BidSize = b.BidSize ?? a.BidSize,
                AskSize = b.AskSize ?? a.AskSize,
                LastSize = b.LastSize ?? a.LastSize,
                Volume = b.Volume ?? a.Volume,
                QuoteTime = b.QuoteTime ?? a.QuoteTime,
                TradeTime = b.TradeTime ?? a.TradeTime,
            };
        }
    }
}
