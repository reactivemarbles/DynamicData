// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

internal readonly record struct StdDev<TValue>(int Count, TValue SumOfItems, TValue SumOfSquares);
