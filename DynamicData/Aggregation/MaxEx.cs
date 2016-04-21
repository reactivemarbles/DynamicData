using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// Maximum and minimum value extensions
    /// 
    /// </summary>
    public static class MaxEx
    {
        #region Abstracted

        /// <summary>
        /// Continually calculates the maximum value from the underlying data sourcce
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The value to use when the underlying collection is empty</param>
        /// <returns>
        /// A distinct observable of the maximum item
        /// </returns>
        public static IObservable<TResult> Maximum<TObject, TResult>([NotNull] this IObservable<IChangeSet<TObject>> source,
                                                                     [NotNull] Func<TObject, TResult> valueSelector,
                                                                     TResult emptyValue = default(TResult))
            where TResult : struct, IComparable<TResult>
        {
            return source.ToChangesAndCollection().Calculate(valueSelector, MaxOrMin.Max, emptyValue);
        }

        /// <summary>
        /// Continually calculates the maximum value from the underlying data sourcce
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The value to use when the underlying collection is empty</param>
        /// <returns>
        /// A distinct observable of the maximum item
        /// </returns>
        public static IObservable<TResult> Maximum<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default(TResult))
            where TResult : struct, IComparable<TResult>
        {
            return source.ToChangesAndCollection().Calculate(valueSelector, MaxOrMin.Max, emptyValue);
        }

        /// <summary>
        /// Continually calculates the minimum value from the underlying data sourcce
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The value to use when the underlying collection is empty</param>
        /// <returns>A distinct observable of the minimums item</returns>
        public static IObservable<TResult> Minimum<TObject, TResult>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default(TResult))
            where TResult : struct, IComparable<TResult>
        {
            return source.ToChangesAndCollection().Calculate(valueSelector, MaxOrMin.Min, emptyValue);
        }

        /// <summary>
        /// Continually calculates the minimum value from the underlying data sourcce
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The value to use when the underlying collection is empty</param>
        /// <returns>
        /// A distinct observable of the minimums item
        /// </returns>
        public static IObservable<TResult> Minimum<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default(TResult))
            where TResult : struct, IComparable<TResult>
        {
            return source.ToChangesAndCollection().Calculate(valueSelector, MaxOrMin.Min, emptyValue);
        }

        #endregion

        private static IObservable<TResult> Calculate<TObject, TResult>([NotNull] this IObservable<ChangesAndCollection<TObject>> source,
                                                                        [NotNull] Func<TObject, TResult> valueSelector,
                                                                        MaxOrMin maxOrMin,
                                                                        TResult emptyValue = default(TResult))
            where TResult : struct, IComparable<TResult>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Scan(default(TResult?), (state, latest) =>
            {
                var current = state;
                var requiresReset = false;

                foreach (var change in latest.Changes)
                {
                    var value = valueSelector(change.Item);
                    if (!current.HasValue)
                    {
                        current = value;
                    }

                    if (change.Type == AggregateType.Add)
                    {
                        int isMatched = maxOrMin == MaxOrMin.Max ? 1 : -1;
                        if (value.CompareTo(current.Value) == isMatched)
                            current = value;
                    }
                    else
                    {
                        //check whether the max / min has been removed. If so we need to look 
                        //up the latest from the underlying collection
                        if (value.CompareTo(current.Value) != 0) continue;
                        requiresReset = true;
                        break;
                    }
                }

                if (requiresReset)
                {
                    var collecton = latest.Collection;
                    if (collecton.Count == 0)
                    {
                        current = default(TResult?);
                    }
                    else
                    {
                        current = maxOrMin == MaxOrMin.Max
                            ? collecton.Max(valueSelector)
                            : collecton.Min(valueSelector);
                    }
                }
                return current;
            })
                         .Select(t => t ?? emptyValue)
                         .DistinctUntilChanged();
        }

        #region Helpers

        private static IObservable<ChangesAndCollection<TObject>> ToChangesAndCollection<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Publish(shared =>
            {
                var changes = shared.ForAggregation();
                var data = shared.ToCollection();
                return data.Zip(changes, (d, c) => new ChangesAndCollection<TObject>(c, d));
            });
        }

        private static IObservable<ChangesAndCollection<TObject>> ToChangesAndCollection<TObject>([NotNull] this IObservable<IChangeSet<TObject>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Publish(shared =>
            {
                var changes = shared.ForAggregation();
                var data = shared.ToCollection();
                return data.Zip(changes, (d, c) => new ChangesAndCollection<TObject>(c, d));
            });
        }

        private enum MaxOrMin
        {
            Max,
            Min
        }

        private class ChangesAndCollection<T>
        {
            public IAggregateChangeSet<T> Changes { get; }
            public IReadOnlyCollection<T> Collection { get; }

            public ChangesAndCollection(IAggregateChangeSet<T> changes, IReadOnlyCollection<T> collection)
            {
                Changes = changes;
                Collection = collection;
            }
        }

        #endregion
    }
}
