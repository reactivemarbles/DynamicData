// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests;

/// <summary>
/// Test extensions.
/// </summary>
public static class ListTextEx
{
    /// <summary>
    /// Aggregates all events and statistics for a change set to help assertions when testing.
    /// </summary>
    /// <param name="source">The source observable.</param>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <returns>The change set aggregator.</returns>
    public static ChangeSetAggregator<T> AsAggregator<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => new(source);
}
