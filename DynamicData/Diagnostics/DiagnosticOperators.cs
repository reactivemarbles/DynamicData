using System;
using System.Diagnostics;
using System.Reactive.Linq;

namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Extensions for diagnostics
    /// </summary>
    public static class DiagnosticOperators
    {

        /// <summary>
        /// Intercepts the specified source enabling the consumer to apply before and after actions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="beforeAction">The before action.</param>
        /// <param name="afterAction">The after action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// beforeAction
        /// or
        /// afterAction
        /// </exception>
        public static IObservable<T> Intercept<T>(this IObservable<T> source, 
            Action<T> beforeAction,
            Action<T> afterAction)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (beforeAction == null) throw new ArgumentNullException("beforeAction");
            if (afterAction == null) throw new ArgumentNullException("afterAction");

            return Observable.Create<T>
                (
                    observer => source.Subscribe(t =>
                    {
                        try
                        {
                            beforeAction(t);
                            observer.OnNext(t);
                            afterAction(t);
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    }, observer.OnError, observer.OnCompleted));
        }


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
            if (source == null) throw new ArgumentNullException("source");
            return source.Scan(new ChangeSummary(), (seed, next) =>
                {
                    int index = seed.Overall.Index + 1;
                    int adds = seed.Overall.Adds + next.Adds;
                    int updates = seed.Overall.Updates + next.Updates;
                    int removes = seed.Overall.Removes + next.Removes;
                    int evaluates = seed.Overall.Evaluates + next.Evaluates;
                    int moves = seed.Overall.Moves + next.Moves;
                    int total = seed.Overall.Count + next.Count;
                    
                    var latest = new ChangeStatistics(index, next.Adds, next.Updates, next.Removes, next.Evaluates,next.Moves,next.Count);
                    var overall = new ChangeStatistics(index, adds, updates, removes, evaluates, moves, total);
                    return new ChangeSummary(index, latest, overall);
                });
        }
    }
}