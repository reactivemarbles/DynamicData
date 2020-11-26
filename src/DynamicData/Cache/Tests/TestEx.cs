// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Test extensions.
    /// </summary>
    public static class TestEx
    {
        /// <summary>
        /// Aggregates all events and statistics for a paged change set to help assertions when testing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The change set aggregator.</returns>
        public static ChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return new(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a distinct change set to help assertions when testing.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The distinct change set aggregator.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static DistinctChangeSetAggregator<TValue> AsAggregator<TValue>(this IObservable<IDistinctChangeSet<TValue>> source)
            where TValue : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new DistinctChangeSetAggregator<TValue>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a sorted change set to help assertions when testing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The sorted change set aggregator.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static SortedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new SortedChangeSetAggregator<TObject, TKey>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a virtual change set to help assertions when testing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The virtual change set aggregator.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static VirtualChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new VirtualChangeSetAggregator<TObject, TKey>(source);
        }

        /// <summary>
        /// Aggregates all events and statistics for a paged change set to help assertions when testing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The paged change set aggregator.</returns>
        public static PagedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new PagedChangeSetAggregator<TObject, TKey>(source);
        }
    }
}