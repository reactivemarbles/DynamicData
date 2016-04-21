using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// Aggregation extensions
    /// </summary>
    public static class AggregationEx
    {
        /// <summary>
        /// Transforms the changeset into an enumerable which is suitable for high performing aggregations
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IAggregateChangeSet<TObject>> ForAggregation<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changeset => (IAggregateChangeSet<TObject>)new AggregateEnumerator<TObject, TKey>(changeset));
        }

        /// <summary>
        /// Transforms the changeset into an enumerable which is suitable for high performing aggregations
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IAggregateChangeSet<TObject>> ForAggregation<TObject>([NotNull] this IObservable<IChangeSet<TObject>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changeset => (IAggregateChangeSet<TObject>)new AggregateEnumerator<TObject>(changeset));
        }

        /// <summary>
        /// Applies an accumulator when items are added to and removed from specified stream,  
        /// starting with the initial seed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accessor">The accessor.</param>
        /// <param name="addAction">The add action.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        internal static IObservable<TResult> Accumlate<TObject, TResult>([NotNull] this IObservable<IChangeSet<TObject>> source,
                                                                         TResult seed,
                                                                         [NotNull] Func<TObject, TResult> accessor,
                                                                         [NotNull] Func<TResult, TResult, TResult> addAction,
                                                                         [NotNull] Func<TResult, TResult, TResult> removeAction)
        {
            return source.ForAggregation().Accumlate(seed, accessor, addAction, removeAction);
        }

        /// <summary>
        /// Applies an accumulator when items are added to and removed from specified stream,  
        /// starting with the initial seed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accessor">The accessor.</param>
        /// <param name="addAction">The add action.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        internal static IObservable<TResult> Accumlate<TObject, TKey, TResult>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                                               TResult seed,
                                                                               [NotNull] Func<TObject, TResult> accessor,
                                                                               [NotNull] Func<TResult, TResult, TResult> addAction,
                                                                               [NotNull] Func<TResult, TResult, TResult> removeAction)
        {
            return source.ForAggregation().Accumlate(seed, accessor, addAction, removeAction);
        }

        /// <summary>
        /// Applies an accumulator when items are added to and removed from specified stream,  
        /// starting with the initial seed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accessor">The accessor.</param>
        /// <param name="addAction">The add action.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        internal static IObservable<TResult> Accumlate<TObject, TResult>([NotNull] this IObservable<IAggregateChangeSet<TObject>> source,
                                                                         TResult seed,
                                                                         [NotNull] Func<TObject, TResult> accessor,
                                                                         [NotNull] Func<TResult, TResult, TResult> addAction,
                                                                         [NotNull] Func<TResult, TResult, TResult> removeAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            if (addAction == null) throw new ArgumentNullException(nameof(addAction));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));

            return source.Scan(seed, (state, changes) =>
            {
                return changes.Aggregate(state, (current, aggregateItem) =>
                                                    aggregateItem.Type == AggregateType.Add
                                                        ? addAction(current, accessor(aggregateItem.Item))
                                                        : removeAction(current, accessor(aggregateItem.Item))
                    );
            });
        }

        /// <summary>
        /// Used to invalidate an aggregating stream. Used when there has been an inline change 
        /// i.e. a property changed or meta data has changed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="invalidate">The invalidate.</param>
        /// <returns></returns>
        public static IObservable<T> InvalidateWhen<T>(this IObservable<T> source, IObservable<Unit> invalidate)
        {
            return invalidate.StartWith(Unit.Default)
                             .Select(_ => source)
                             .Switch()
                             .DistinctUntilChanged();
        }

        /// <summary>
        /// Used to invalidate an aggregating stream. Used when there has been an inline change 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TTrigger">The type of the trigger.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="invalidate">The invalidate.</param>
        /// <returns></returns>
        public static IObservable<T> InvalidateWhen<T, TTrigger>(this IObservable<T> source, IObservable<TTrigger> invalidate)
        {
            return invalidate.StartWith(default(TTrigger))
                             .Select(_ => source)
                             .Switch()
                             .DistinctUntilChanged();
        }
    }
}
