using System;
using System.Reactive.Linq;

namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Extensions for diagnostics
    /// </summary>
    public static class DiagnosticOperators
    {
        /// <summary>
        /// Accumulates update statistics
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ChangeSummary> CollectUpdateStats<TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Scan(ChangeSummary.Empty, (seed, next) =>
            {
                int index = seed.Overall.Index + 1;
                int adds = seed.Overall.Adds + next.Adds;
                int updates = seed.Overall.Updates + next.Updates;
                int removes = seed.Overall.Removes + next.Removes;
                int evaluates = seed.Overall.Refreshes + next.Refreshes;
                int moves = seed.Overall.Moves + next.Moves;
                int total = seed.Overall.Count + next.Count;

                var latest = new ChangeStatistics(index, next.Adds, next.Updates, next.Removes, next.Refreshes, next.Moves, next.Count);
                var overall = new ChangeStatistics(index, adds, updates, removes, evaluates, moves, total);
                return new ChangeSummary(index, latest, overall);
            });
        }

        /// <summary>
        /// Accumulates update statistics
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ChangeSummary> CollectUpdateStats<TSource>(this IObservable<IChangeSet<TSource>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Scan(ChangeSummary.Empty, (seed, next) =>
            {
                int index = seed.Overall.Index + 1;
                int adds = seed.Overall.Adds + next.Adds;
                int updates = seed.Overall.Updates + next.Replaced;
                int removes = seed.Overall.Removes + next.Removes;
                int moves = seed.Overall.Moves + next.Moves;
                int total = seed.Overall.Count + next.Count;

                var latest = new ChangeStatistics(index, next.Adds, next.Replaced, next.Removes, 0, next.Moves, next.Count);
                var overall = new ChangeStatistics(index, adds, updates, removes, 0, moves, total);
                return new ChangeSummary(index, latest, overall);
            });
        }
    }
}
