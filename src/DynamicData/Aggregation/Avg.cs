// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Aggregation;
#else

namespace DynamicData.Aggregation;
#endif

/// <summary>
/// Represents the Avg record.
/// </summary>
/// <typeparam name="TValue">The type of the TValue value.</typeparam>
/// <param name="Count">The Count value.</param>
/// <param name="Sum">The Sum value.</param>
internal readonly record struct Avg<TValue>(int Count, TValue Sum);
