// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// A change set which has been shaped for rapid online aggregations.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IAggregateChangeSet<T> : IEnumerable<AggregateItem<T>>
{
}
