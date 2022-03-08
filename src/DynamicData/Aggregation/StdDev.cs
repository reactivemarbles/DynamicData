// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

internal readonly struct StdDev<TValue>
{
    public StdDev(int count, TValue sumOfItems, TValue sumOfSquares)
    {
        Count = count;
        SumOfItems = sumOfItems;
        SumOfSquares = sumOfSquares;
    }

    public int Count { get; }

    public TValue SumOfItems { get; }

    public TValue SumOfSquares { get; }
}
