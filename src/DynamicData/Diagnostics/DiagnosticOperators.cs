// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Diagnostics;

/// <summary>
/// Extensions for diagnostics.
/// </summary>
public static class DiagnosticOperators
{
    /// <summary>
    /// Accumulates update statistics.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change summary.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ChangeSummary> CollectUpdateStats<TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source)
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Scan(
            ChangeSummary.Empty,
            (seed, next) =>
            {
                var index = seed.Overall.Index + 1;
                var adds = seed.Overall.Adds + next.Adds;
                var updates = seed.Overall.Updates + next.Updates;
                var removes = seed.Overall.Removes + next.Removes;
                var evaluates = seed.Overall.Refreshes + next.Refreshes;
                var moves = seed.Overall.Moves + next.Moves;
                var total = seed.Overall.Count + next.Count;

                var latest = new ChangeStatistics(index, next.Adds, next.Updates, next.Removes, next.Refreshes, next.Moves, next.Count);
                var overall = new ChangeStatistics(index, adds, updates, removes, evaluates, moves, total);
                return new ChangeSummary(index, latest, overall);
            });
    }

    /// <summary>
    /// Accumulates update statistics.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change summary.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ChangeSummary> CollectUpdateStats<TSource>(this IObservable<IChangeSet<TSource>> source)
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Scan(
            ChangeSummary.Empty,
            (seed, next) =>
            {
                var index = seed.Overall.Index + 1;
                var adds = seed.Overall.Adds + next.Adds;
                var updates = seed.Overall.Updates + next.Replaced;
                var removes = seed.Overall.Removes + next.Removes;
                var moves = seed.Overall.Moves + next.Moves;
                var total = seed.Overall.Count + next.Count;

                var latest = new ChangeStatistics(index, next.Adds, next.Replaced, next.Removes, 0, next.Moves, next.Count);
                var overall = new ChangeStatistics(index, adds, updates, removes, 0, moves, total);
                return new ChangeSummary(index, latest, overall);
            });
    }
}
