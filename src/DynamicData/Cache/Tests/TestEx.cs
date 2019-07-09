using System;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Test extensions
    /// </summary>
    public static class TestEx
    {
        /// <summary>
        /// Aggregates all events and statistics for a paged changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static ChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return new ChangeSetAggregator<TObject, TKey>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a distinct changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static DistinctChangeSetAggregator<TValue> AsAggregator<TValue>(this IObservable<IDistinctChangeSet<TValue>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new DistinctChangeSetAggregator<TValue>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a sorted changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static SortedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SortedChangeSetAggregator<TObject, TKey>(source);
        }

        /// <summary>
        ///Aggregates all events and statistics for a virtual changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static VirtualChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new VirtualChangeSetAggregator<TObject, TKey>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a paged changeset to help assertions when testing
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static PagedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new PagedChangeSetAggregator<TObject, TKey>(source);
        }
    }
}
