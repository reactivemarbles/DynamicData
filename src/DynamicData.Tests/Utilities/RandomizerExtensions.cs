using System;
using System.Diagnostics;
using Bogus;

namespace DynamicData.Tests.Utilities;

internal static class RandomizerExtensions
{
    public static TimeSpan TimeSpan(this Randomizer randomizer, TimeSpan minTime, TimeSpan maxTime) => System.TimeSpan.FromTicks(randomizer.Long(minTime.Ticks, maxTime.Ticks));

    public static TimeSpan TimeSpan(this Randomizer randomizer, TimeSpan maxTime) => TimeSpan(randomizer, System.TimeSpan.Zero, maxTime);

    public static bool CoinFlip(this Randomizer randomizer, Action action)
    {
        if (randomizer.Bool())
        {
            action();
            return true;
        }

        return false;
    }

    public static bool Chance(this Randomizer randomizer, double chancePercent, Action action)
    {
        Debug.Assert(chancePercent >= 0.0 && chancePercent <= 1.0);
        if (randomizer.Double() <= chancePercent)
        {
            action();
            return true;
        }

        return false;
    }
}
