// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

internal readonly struct Avg<TValue>(int count, TValue sum)
{
    public int Count { get; } = count;

    public TValue Sum { get; } = sum;
}
