// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Aggregation;
#else

namespace DynamicData.Aggregation;
#endif

/// <summary>
/// Represents the StdDev record.
/// </summary>
/// <typeparam name="T">The numeric accumulator type.</typeparam>
/// <param name="Count">The Count value.</param>
/// <param name="Mean">The Mean value.</param>
/// <param name="SumOfSquaredDifferences">The SumOfSquaredDifferences value.</param>
internal readonly record struct StdDev<T>(int Count, T Mean, T SumOfSquaredDifferences);
