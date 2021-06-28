// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Extensions for dynamic data.
    /// </summary>
    public static class ObservableCacheEx
    {
        private const int DefaultSortResetThreshold = 100;

        /// <summary>
        /// Inject side effects into the stream using the specified adaptor.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (adaptor is null)
            {
                throw new ArgumentNullException(nameof(adaptor));
            }

            return source.Do(adaptor.Adapt);
        }

        /// <summary>
        /// Inject side effects into the stream using the specified sorted adaptor.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (adaptor is null)
            {
                throw new ArgumentNullException(nameof(adaptor));
            }

            return source.Do(adaptor.Adapt);
        }

        /// <summary>
        /// Adds or updates the cache with the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.AddOrUpdate(item));
        }

        /// <summary>
        /// Adds or updates the cache with the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <param name="equalityComparer">The equality comparer used to determine whether a new item is the same as an existing cached item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item, IEqualityComparer<TObject> equalityComparer)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.AddOrUpdate(item, equalityComparer));
        }

        /// <summary>
        /// <summary>
        /// Adds or updates the cache with the specified items.
        /// </summary>
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.AddOrUpdate(items));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source cache.</param>
        /// <param name="item">The item to add or update.</param>
        /// <param name="key">The key to add or update.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void AddOrUpdate<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TObject item, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            source.Edit(updater => updater.AddOrUpdate(item, key));
        }

        /// <summary>
        /// Applied a logical And operator between the collections i.e items which are in all of the
        /// sources are included.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source or others.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (others is null || others.Length == 0)
            {
                throw new ArgumentNullException(nameof(others));
            }

            return source.Combine(CombineOperator.And, others);
        }

        /// <summary>
        /// Applied a logical And operator between the collections i.e items which are in all of the sources are included.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Converts the source to an read only observable cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable cache.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new AnonymousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Converts the source to a readonly observable cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="applyLocking">if set to <c>true</c> all methods are synchronised. There is no need to apply locking when the consumer can be sure the read / write operations are already synchronised.</param>
        /// <returns>An observable cache.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, bool applyLocking = true)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (applyLocking)
            {
                return new AnonymousObservableCache<TObject, TKey>(source);
            }

            return new LockFreeObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Automatically refresh downstream operators when any properties change.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable.</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
        /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable change set with additional refresh changes.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.AutoRefreshOnObservable(
                (t, _) =>
                    {
                        if (propertyChangeThrottle is null)
                        {
                            return t.WhenAnyPropertyChanged();
                        }

                        return t.WhenAnyPropertyChanged().Throttle(propertyChangeThrottle.Value, scheduler ?? Scheduler.Default);
                    },
                changeSetBuffer,
                scheduler);
        }

        /// <summary>
        /// Automatically refresh downstream operators when properties change.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source observable.</param>
        /// <param name="propertyAccessor">Specify a property to observe changes. When it changes a Refresh is invoked.</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
        /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable change set with additional refresh changes.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey, TProperty>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TProperty>> propertyAccessor, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.AutoRefreshOnObservable(
                (t, _) =>
                    {
                        if (propertyChangeThrottle is null)
                        {
                            return t.WhenPropertyChanged(propertyAccessor, false);
                        }

                        return t.WhenPropertyChanged(propertyAccessor, false).Throttle(propertyChangeThrottle.Value, scheduler ?? Scheduler.Default);
                    },
                changeSetBuffer,
                scheduler);
        }

        /// <summary>
        /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <typeparam name="TAny">The type of evaluation.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <param name="reevaluator">An observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable change set with additional refresh changes.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return source.AutoRefreshOnObservable((t, _) => reevaluator(t), changeSetBuffer, scheduler);
        }

        /// <summary>
        /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <typeparam name="TAny">The type of evaluation.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <param name="reevaluator">An observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable change set with additional refresh changes.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (reevaluator is null)
            {
                throw new ArgumentNullException(nameof(reevaluator));
            }

            return new AutoRefresh<TObject, TKey, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
        }

        /// <summary>
        /// Batches the updates for the specified time period.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSpan">The time span.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// scheduler.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan timeSpan, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Buffer(timeSpan, scheduler ?? Scheduler.Default).FlattenBufferResult();
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return BatchIf(source, pauseIfTrueSelector, false, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
        /// <param name="initialPauseState">if set to <c>true</c> [initial pause state].</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, scheduler: scheduler).Run();
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long. On completion buffering will cease.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut = null, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
        /// <param name="initialPauseState">if set to <c>true</c> [initial pause state].</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long. On completion buffering will cease.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (pauseIfTrueSelector is null)
            {
                throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            }

            return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, timeOut, initialPauseState, scheduler: scheduler).Run();
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
        /// <param name="initialPauseState">if set to <c>true</c> [initial pause state].</param>
        /// <param name="timer">Specify a time observable. The buffer will be emptied each time the timer produces a value and when it completes. On completion buffering will cease.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IObservable<Unit>? timer = null, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, timer, scheduler).Run();
        }

        /// <summary>
        ///  Binds the results to the specified observable collection using the default update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var updater = new ObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }

        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, IObservableCollectionAdaptor<TObject, TKey> updater)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (updater is null)
            {
                throw new ArgumentNullException(nameof(updater));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var locker = new object();
                        return source.Synchronize(locker).Select(
                            changes =>
                                {
                                    updater.Adapt(changes, destination);
                                    return changes;
                                }).SubscribeSafe(observer);
                    });
        }

        /// <summary>
        ///  Binds the results to the specified observable collection using the default update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var updater = new SortedObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }

        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, ISortedObservableCollectionAdaptor<TObject, TKey> updater)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (updater is null)
            {
                throw new ArgumentNullException(nameof(updater));
            }

            return Observable.Create<ISortedChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var locker = new object();
                        return source.Synchronize(locker).Select(
                            changes =>
                                {
                                    updater.Adapt(changes, destination);
                                    return changes;
                                }).SubscribeSafe(observer);
                    });
        }

        /// <summary>
        /// Binds the results to the specified readonly observable collection using the default update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
        /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection.</param>
        /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = 25, ISortedObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var target = new ObservableCollectionExtended<TObject>();
            var result = new ReadOnlyObservableCollection<TObject>(target);
            var updater = adaptor ?? new SortedObservableCollectionAdaptor<TObject, TKey>(resetThreshold);
            readOnlyObservableCollection = result;
            return source.Bind(target, updater);
        }

        /// <summary>
        /// Binds the results to the specified readonly observable collection using the default update algorithm.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
        /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection.</param>
        /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = 25, IObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var target = new ObservableCollectionExtended<TObject>();
            var result = new ReadOnlyObservableCollection<TObject>(target);
            var updater = adaptor ?? new ObservableCollectionAdaptor<TObject, TKey>(resetThreshold);
            readOnlyObservableCollection = result;
            return source.Bind(target, updater);
        }

#if SUPPORTS_BINDINGLIST

        /// <summary>
        /// Binds a clone of the observable change set to the target observable collection.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="bindingList">The target binding list.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// targetCollection.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = 25)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (bindingList is null)
            {
                throw new ArgumentNullException(nameof(bindingList));
            }

            return source.Adapt(new BindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
        }

#endif

#if SUPPORTS_BINDINGLIST

        /// <summary>
        /// Binds a clone of the observable change set to the target observable collection.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="bindingList">The target binding list.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>An observable which will emit change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// targetCollection.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = 25)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (bindingList is null)
            {
                throw new ArgumentNullException(nameof(bindingList));
            }

            return source.Adapt(new SortedBindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
        }

#endif

        /// <summary>
        /// Buffers changes for an initial period only. After the period has elapsed, not further buffering occurs.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source change set.</param>
        /// <param name="initialBuffer">The period to buffer, measure from the time that the first item arrives.</param>
        /// <param name="scheduler">The scheduler to buffer on.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> BufferInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return source.DeferUntilLoaded().Publish(
                shared =>
                    {
                        var initial = shared.Buffer(initialBuffer, scheduler ?? Scheduler.Default).FlattenBufferResult().Take(1);

                        return initial.Concat(shared);
                    });
        }

        /// <summary>
        /// Cast the object to the specified type.
        /// Alas, I had to add the converter due to type inference issues.
        /// </summary>
        /// <typeparam name="TSource">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="converter">The conversion factory.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new Cast<TSource, TKey, TDestination>(source, converter).Run();
        }

        /// <summary>
        /// Changes the primary key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector eg. (item) => newKey.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TObject, TDestinationKey> keySelector)
            where TSourceKey : notnull
            where TDestinationKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return source.Select(
                updates =>
                    {
                        var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                        return new ChangeSet<TObject, TDestinationKey>(changed);
                    });
        }

        /// <summary>
        /// Changes the primary key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector eg. (key, item) => newKey.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TSourceKey, TObject, TDestinationKey> keySelector)
            where TSourceKey : notnull
            where TDestinationKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return source.Select(
                updates =>
                    {
                        var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Key, u.Current), u.Current, u.Previous));
                        return new ChangeSet<TObject, TDestinationKey>(changed);
                    });
        }

        /// <summary>
        /// Clears all data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Clear());
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Clear());
        }

        /// <summary>
        /// Clears all data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Clear<TObject, TKey>(this LockFreeObservableCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Clear());
        }

        /// <summary>
        /// Clones the changes  into the specified collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ICollection<TObject> target)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return source.Do(
                changes =>
                    {
                        foreach (var item in changes)
                        {
                            switch (item.Reason)
                            {
                                case ChangeReason.Add:
                                    {
                                        target.Add(item.Current);
                                    }

                                    break;

                                case ChangeReason.Update:
                                    {
                                        target.Remove(item.Previous.Value);
                                        target.Add(item.Current);
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    target.Remove(item.Current);
                                    break;
                            }
                        }
                    });
        }

        /// <summary>
        /// Convert the object using the specified conversion function.
        /// This is a lighter equivalent of Transform and is designed to be used with non-disposable objects.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns>An observable which emits change sets.</returns>
        [Obsolete("This was an experiment that did not work. Use Transform instead")]
        public static IObservable<IChangeSet<TDestination, TKey>> Convert<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TDestination> conversionFactory)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (conversionFactory is null)
            {
                throw new ArgumentNullException(nameof(conversionFactory));
            }

            return source.Select(
                changes =>
                    {
                        var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, conversionFactory(change.Current), change.Previous.Convert(conversionFactory), change.CurrentIndex, change.PreviousIndex));
                        return new ChangeSet<TDestination, TKey>(transformed);
                    });
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new DeferUntilLoaded<TObject, TKey>(source).Run();
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new DeferUntilLoaded<TObject, TKey>(source).Run();
        }

        /// <summary>
        /// Disposes each item when no longer required.
        ///
        /// Individual items are disposed when removed or replaced. All items
        /// are disposed when the stream is disposed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>A continuation of the original stream.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new DisposeMany<TObject, TKey>(
                source,
                t =>
                    {
                        var d = t as IDisposable;
                        d?.Dispose();
                    }).Run();
        }

        /// <summary>
        ///     Selects distinct values from the source.
        /// </summary>
        /// <typeparam name="TObject">The type object from which the distinct values are selected.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns>An observable which will emit distinct change sets.</returns>
        /// <remarks>
        /// Due to it's nature only adds or removes can be returned.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
            where TKey : notnull
            where TValue : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (valueSelector is null)
            {
                throw new ArgumentNullException(nameof(valueSelector));
            }

            return Observable.Create<IDistinctChangeSet<TValue>>(observer => new DistinctCalculator<TObject, TKey, TValue>(source, valueSelector).Run().SubscribeSafe(observer));
        }

        /// <summary>
        /// Loads the cache with the specified items in an optimised manner i.e. calculates the differences between the old and new items
        ///  in the list and amends only the differences.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="allItems">The items to add, update or delete.</param>
        /// <param name="equalityComparer">The equality comparer used to determine whether a new item is the same as an existing cached item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, IEqualityComparer<TObject> equalityComparer)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (allItems is null)
            {
                throw new ArgumentNullException(nameof(allItems));
            }

            if (equalityComparer is null)
            {
                throw new ArgumentNullException(nameof(equalityComparer));
            }

            source.EditDiff(allItems, equalityComparer.Equals);
        }

        /// <summary>
        /// Loads the cache with the specified items in an optimised manner i.e. calculates the differences between the old and new items
        ///  in the list and amends only the differences.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="allItems">The items to compare and add, update or delete.</param>
        /// <param name="areItemsEqual">Expression to determine whether an item's value is equal to the old value (current, previous) => current.Version == previous.Version.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, Func<TObject, TObject, bool> areItemsEqual)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (allItems is null)
            {
                throw new ArgumentNullException(nameof(allItems));
            }

            if (areItemsEqual is null)
            {
                throw new ArgumentNullException(nameof(areItemsEqual));
            }

            var editDiff = new EditDiff<TObject, TKey>(source, areItemsEqual);
            editDiff.Edit(allItems);
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        [Obsolete(Constants.EvaluateIsDead)]
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh(item));
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        [Obsolete(Constants.EvaluateIsDead)]
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh(items));
        }

        /// <summary>
        /// Signal observers to re-evaluate the all items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        [Obsolete(Constants.EvaluateIsDead)]
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh());
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections
        /// Items from the first collection in the outer list are included unless contained in any of the other lists.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (others is null || others.Length == 0)
            {
                throw new ArgumentNullException(nameof(others));
            }

            return source.Combine(CombineOperator.Except, others);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections
        /// Items from the first collection in the outer list are included unless contained in any of the other lists.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The sources.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections
        /// Items from the first collection in the outer list are included unless contained in any of the other lists.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Automatically removes items from the stream after the time specified by
        /// the timeSelector elapses.  Return null if the item should never be removed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">The time selector.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// timeSelector.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector)
            where TKey : notnull
        {
            return ExpireAfter(source, timeSelector, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the stream after the time specified by
        /// the timeSelector elapses.  Return null if the item should never be removed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">The time selector.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// timeSelector.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (timeSelector is null)
            {
                throw new ArgumentNullException(nameof(timeSelector));
            }

            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval)
            where TKey : notnull
        {
            return ExpireAfter(source, timeSelector, pollingInterval, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (timeSelector is null)
            {
                throw new ArgumentNullException(nameof(timeSelector));
            }

            return new TimeExpirer<TObject, TKey>(source, timeSelector, pollingInterval, scheduler).ExpireAfter();
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="scheduler">The scheduler to perform the work on.</param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TObject, TimeSpan?> timeSelector, IScheduler? scheduler = null)
            where TKey : notnull
        {
            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="interval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval .
        /// </param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval = null)
            where TKey : notnull
        {
            return ExpireAfter(source, timeSelector, interval, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="pollingInterval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Deliberate capture.")]
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler? scheduler)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (timeSelector is null)
            {
                throw new ArgumentNullException(nameof(timeSelector));
            }

            scheduler ??= Scheduler.Default;

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(
                observer =>
                    {
                        return source.Connect().ForExpiry(timeSelector, pollingInterval, scheduler).Finally(observer.OnCompleted).Subscribe(
                            toRemove =>
                                {
                                    try
                                    {
                                        // remove from cache and notify which items have been auto removed
                                        var keyValuePairs = toRemove as KeyValuePair<TKey, TObject>[] ?? toRemove.AsArray();
                                        if (keyValuePairs.Length == 0)
                                        {
                                            return;
                                        }

                                        source.Remove(keyValuePairs.Select(kv => kv.Key));
                                        observer.OnNext(keyValuePairs);
                                    }
                                    catch (Exception ex)
                                    {
                                        observer.OnError(ex);
                                    }
                                });
                    });
        }

        /// <summary>
        /// Filters the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new StaticFilter<TObject, TKey>(source, filter).Run();
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicateChanged is null)
            {
                throw new ArgumentNullException(nameof(predicateChanged));
            }

            return new DynamicFilter<TObject, TKey>(source, predicateChanged).Run();
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Unit> reapplyFilter)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (reapplyFilter is null)
            {
                throw new ArgumentNullException(nameof(reapplyFilter));
            }

            var empty = Observable.Empty<Func<TObject, bool>>();
            return new DynamicFilter<TObject, TKey>(source, empty, reapplyFilter).Run();
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, IObservable<Unit> reapplyFilter)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicateChanged is null)
            {
                throw new ArgumentNullException(nameof(predicateChanged));
            }

            if (reapplyFilter is null)
            {
                throw new ArgumentNullException(nameof(reapplyFilter));
            }

            return new DynamicFilter<TObject, TKey>(source, predicateChanged, reapplyFilter).Run();
        }

        /// <summary>
        /// Filters source on the specified property using the specified predicate.
        /// The filter will automatically reapply when a property changes.
        /// When there are likely to be a large number of property changes specify a throttle to improve performance.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector. When the property changes a the filter specified will be re-evaluated.</param>
        /// <param name="predicate">A predicate based on the object which contains the changed property.</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler used when throttling.</param>
        /// <returns>An observable which emits change sets.</returns>
        [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
        public static IObservable<IChangeSet<TObject, TKey>> FilterOnProperty<TObject, TKey, TProperty>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertySelector is null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return new FilterOnProperty<TObject, TKey, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Ensure that finally is always called. Thanks to Lee Campbell for this.
        /// </summary>
        /// <typeparam name="T">The type contained within the observables.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="finallyAction">The finally action.</param>
        /// <returns>An observable which has always a finally action applied.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        [Obsolete("This can cause unhandled exception issues so do not use")]
        public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (finallyAction is null)
            {
                throw new ArgumentNullException(nameof(finallyAction));
            }

            return new FinallySafe<T>(source, finallyAction).Run();
        }

        /// <summary>
        /// Flattens an update collection to it's individual items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change set values on a flatten result.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.SelectMany(changes => changes);
        }

        /// <summary>
        /// Convert the result of a buffer operation to a single change set.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> FlattenBufferResult<TObject, TKey>(this IObservable<IList<IChangeSet<TObject, TKey>>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Where(x => x.Count != 0).Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));
        }

        /// <summary>
        /// Provides a call back for each change.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="action">The action.</param>
        /// <returns>An observable which will perform the action on each item.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<Change<TObject, TKey>> action)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return source.Do(changes => changes.ForEach(action));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking any left or right values and matching them, provided that the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.FullJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking any left or right values and matching them, provided that the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.FullJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory.
        ///  A group is included for each item in the resulting group source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelector">The group selector factory.</param>
        /// <param name="resultGroupSource">
        ///   A distinct stream used to determine the result.
        /// </param>
        /// <remarks>
        /// Useful for parent-child collection when the parent and child are soured from different streams.
        /// </remarks>
        /// <returns>An observable which will emit group change sets.</returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelector, IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (groupSelector is null)
            {
                throw new ArgumentNullException(nameof(groupSelector));
            }

            if (resultGroupSource is null)
            {
                throw new ArgumentNullException(nameof(resultGroupSource));
            }

            return new SpecifiedGrouper<TObject, TKey, TGroupKey>(source, groupSelector, resultGroupSource).Run();
        }

        /// <summary>
        /// Groups the source on the value returned by group selector factory.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <returns>An observable which will emit group change sets.</returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (groupSelectorKey is null)
            {
                throw new ArgumentNullException(nameof(groupSelectorKey));
            }

            return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, null).Run();
        }

        /// <summary>
        /// Groups the source on the value returned by group selector factory.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="regrouper">Invoke to  the for the grouping to be re-evaluated.</param>
        /// <returns>An observable which will emit group change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController.
        /// </exception>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (groupSelectorKey is null)
            {
                throw new ArgumentNullException(nameof(groupSelectorKey));
            }

            if (regrouper is null)
            {
                throw new ArgumentNullException(nameof(regrouper));
            }

            return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
        }

        /// <summary>
        /// Groups the source using the property specified by the property selector. Groups are re-applied when the property value changed.
        ///
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items.</param>
        /// <param name="propertyChangedThrottle">A time span that indicates the throttle to wait for property change events.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which will emit immutable group change sets.</returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnProperty<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroupKey>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
            where TObject : INotifyPropertyChanged
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertySelector is null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }

            return new GroupOnProperty<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Groups the source using the property specified by the property selector. Each update produces immutable grouping. Groups are re-applied when the property value changed.
        ///
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items.</param>
        /// <param name="propertyChangedThrottle">A time span that indicates the throttle to wait for property change events.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which will emit immutable group change sets.</returns>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroupKey>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
            where TObject : INotifyPropertyChanged
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertySelector is null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }

            return new GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Groups the source on the value returned by group selector factory. Each update produces immutable grouping.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="regrouper">Invoke to  the for the grouping to be re-evaluated.</param>
        /// <returns>An observable which will emit immutable group change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController.
        /// </exception>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
            where TKey : notnull
            where TGroupKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (groupSelectorKey is null)
            {
                throw new ArgumentNullException(nameof(groupSelectorKey));
            }

            return new GroupOnImmutable<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
        }

        /// <summary>
        /// Ignores updates when the update is the same reference.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable which emits change sets.</param>
        /// <returns>An observable which emits change sets and ignores equal value changes.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> IgnoreSameReferenceUpdate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.IgnoreUpdateWhen((c, p) => ReferenceEquals(c, p));
        }

        /// <summary>
        /// Ignores the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="ignoreFunction">The ignore function (current,previous)=>{ return true to ignore }.</param>
        /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> IgnoreUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> ignoreFunction)
            where TKey : notnull
        {
            return source.Select(
                updates =>
                    {
                        var result = updates.Where(
                            u =>
                                {
                                    if (u.Reason != ChangeReason.Update)
                                    {
                                        return true;
                                    }

                                    return !ignoreFunction(u.Current, u.Previous.Value);
                                });
                        return new ChangeSet<TObject, TKey>(result);
                    }).NotEmpty();
        }

        /// <summary>
        /// Only includes the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="includeFunction">The include function (current,previous)=>{ return true to include }.</param>
        /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> includeFunction)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (includeFunction is null)
            {
                throw new ArgumentNullException(nameof(includeFunction));
            }

            return source.Select(
                changes =>
                    {
                        var result = changes.Where(change => change.Reason != ChangeReason.Update || includeFunction(change.Current, change.Previous.Value));
                        return new ChangeSet<TObject, TKey>(result);
                    }).NotEmpty();
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking values when both left and right values are present
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, TRight, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.InnerJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        ///  Groups the right data source and joins the to the left and the right sources, taking values when both left and right values are present
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<(TLeftKey leftKey, TRightKey rightKey), TLeft, TRight, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left and right have matching values.
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.InnerJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left and right have matching values.
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Invokes Refresh method for an object which implements IEvaluateAware.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TObject : IEvaluateAware
            where TKey : notnull
        {
            return source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Refresh).ForEach(u => u.Current.Evaluate()));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all left values and combining any matching right values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, Optional<TRight>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.LeftJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all left values and combining any matching right values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, Optional<TRight>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, taking all left values and combining any matching right values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.LeftJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, taking all left values and combining any matching right values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Applies a size limiter to the number of records which can be included in the
        /// underlying cache.  When the size limit is reached the oldest items are removed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        /// <exception cref="System.ArgumentException">size cannot be zero.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> LimitSizeTo<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, int size)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (size <= 0)
            {
                throw new ArgumentException("Size limit must be greater than zero");
            }

            return new SizeExpirer<TObject, TKey>(source, size).Run();
        }

        /// <summary>
        /// Limits the number of records in the cache to the size specified.  When the size is reached
        /// the oldest items are removed from the cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable which emits the key value pairs.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        /// <exception cref="System.ArgumentException">Size limit must be greater than zero.</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source, int sizeLimit, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (sizeLimit <= 0)
            {
                throw new ArgumentException("Size limit must be greater than zero", nameof(sizeLimit));
            }

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(
                observer =>
                    {
                        long orderItemWasAdded = -1;
                        var sizeLimiter = new SizeLimiter<TObject, TKey>(sizeLimit);

                        return source.Connect().Finally(observer.OnCompleted).ObserveOn(scheduler ?? Scheduler.Default).Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded))).Select(sizeLimiter.CloneAndReturnExpiredOnly).Where(expired => expired.Length != 0).Subscribe(
                            toRemove =>
                                {
                                    try
                                    {
                                        source.Remove(toRemove.Select(kv => kv.Key));
                                        observer.OnNext(toRemove);
                                    }
                                    catch (Exception ex)
                                    {
                                        observer.OnError(ex);
                                    }
                                });
                    });
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and un-merges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns>An observable which emits the transformed value.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector.</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (observableSelector is null)
            {
                throw new ArgumentNullException(nameof(observableSelector));
            }

            return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and un-merges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns>An observable which emits the transformed value.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector.</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (observableSelector is null)
            {
                throw new ArgumentNullException(nameof(observableSelector));
            }

            return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and un-merges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns>An observable which emits the item with the value.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector.</exception>
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (observableSelector is null)
            {
                throw new ArgumentNullException(nameof(observableSelector));
            }

            return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and un-merges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns>An observable which emits the item with the value.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector.</exception>
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (observableSelector is null)
            {
                throw new ArgumentNullException(nameof(observableSelector));
            }

            return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Monitors the status of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the source observable.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which monitors the status of the observable.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source)
        {
            return new StatusMonitor<T>(source).Run();
        }

        /// <summary>
        /// Suppresses updates which are empty.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change set values when not empty.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Where(changes => changes.Count != 0);
        }

        /// <summary>
        /// Callback for each item as and when it is being added to the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="addAction">The add action.</param>
        /// <returns>An observable which emits a change set with items being added.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> addAction)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (addAction is null)
            {
                throw new ArgumentNullException(nameof(addAction));
            }

            return source.Do(changes => changes.Where(c => c.Reason == ChangeReason.Add).ForEach(c => addAction(c.Current)));
        }

        /// <summary>
        /// Callback for each item as and when it is being removed from the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns>An observable which emits a change set with items being removed.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// removeAction.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (removeAction is null)
            {
                throw new ArgumentNullException(nameof(removeAction));
            }

            return source.Do(changes => changes.Where(c => c.Reason == ChangeReason.Remove).ForEach(c => removeAction(c.Current)));
        }

        /// <summary>
        /// Callback when an item has been updated eg. (current, previous)=>{}.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updateAction">The update action.</param>
        /// <returns>An observable which emits a change set with items being updated.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            return source.Do(changes => changes.Where(c => c.Reason == ChangeReason.Update).ForEach(c => updateAction(c.Current, c.Previous.Value)));
        }

        /// <summary>
        /// Apply a logical Or operator between the collections i.e items which are in any of the sources are included.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (others is null || others.Length == 0)
            {
                throw new ArgumentNullException(nameof(others));
            }

            return source.Combine(CombineOperator.Or, others);
        }

        /// <summary>
        /// Apply a logical Or operator between the collections i.e items which are in any of the sources are included.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Returns the page as specified by the pageRequests observable.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pageRequests">The page requests.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IPagedChangeSet<TObject, TKey>> Page<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IPageRequest> pageRequests)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (pageRequests is null)
            {
                throw new ArgumentNullException(nameof(pageRequests));
            }

            return new Page<TObject, TKey>(source, pageRequests).Run();
        }

        /// <summary>
        /// Populate a cache from an observable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <returns>A disposable which will unsubscribe from the source.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector.
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return observable.Subscribe(source.AddOrUpdate);
        }

        /// <summary>
        /// Populate a cache from an observable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <returns>A disposable which will unsubscribe from the source.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector.
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return observable.Subscribe(source.AddOrUpdate);
        }

        /// <summary>
        /// Populates a source into the specified cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>A disposable which will unsubscribe from the source.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination.
        /// </exception>
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> destination)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
        }

        /// <summary>
        /// Populates a source into the specified cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>A disposable which will unsubscribe from the source.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// destination.</exception>
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> destination)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
        }

        /// <summary>
        /// Populates a source into the specified cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>A disposable which will unsubscribe from the source.</returns>
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, LockFreeObservableCache<TObject, TKey> destination)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
        }

        /// <summary>
        ///  The latest copy of the cache is exposed for querying after each modification to the underlying data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="resultSelector">The result selector.</param>
        /// <returns>An observable which emits the destination values.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// resultSelector.
        /// </exception>
        public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<IQuery<TObject, TKey>, TDestination> resultSelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return source.QueryWhenChanged().Select(resultSelector);
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits the query.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new QueryWhenChanged<TObject, TKey, Unit>(source).Run();
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) on subscription.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemChangedTrigger">Should the query be triggered for observables on individual items.</param>
        /// <returns>An observable that emits the query.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (itemChangedTrigger is null)
            {
                throw new ArgumentNullException(nameof(itemChangedTrigger));
            }

            return new QueryWhenChanged<TObject, TKey, TValue>(source, itemChangedTrigger).Run();
        }

        /// <summary>
        /// Cache equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets that are ref counted.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> RefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new RefCount<TObject, TKey>(source).Run();
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh(item));
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh(items));
        }

        /// <summary>
        /// Signal observers to re-evaluate the all items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Refresh());
        }

        /// <summary>
        /// Removes the specified item from the cache.
        ///
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(item));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(key));
        }

        /// <summary>
        /// Removes the specified items from the cache.
        ///
        /// Any items not contained in the cache are ignored.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(items));
        }

        /// <summary>
        /// Removes the specified keys from the cache.
        ///
        /// Any keys not contained in the cache are ignored.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(keys));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(key));
        }

        /// <summary>
        /// Removes the specified keys from the cache.
        ///
        /// Any keys not contained in the cache are ignored.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.Remove(keys));
        }

        /// <summary>
        /// Removes the key which enables all observable list features of dynamic data.
        /// </summary>
        /// <remarks>
        /// All indexed changes are dropped i.e. sorting is not supported by this function.
        /// </remarks>
        /// <typeparam name="TObject">The type of  object.</typeparam>
        /// <typeparam name="TKey">The type of  key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject>> RemoveKey<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Select(
                changes =>
                    {
                        var enumerator = new RemoveKeyEnumerator<TObject, TKey>(changes);
                        return new ChangeSet<TObject>(enumerator);
                    });
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void RemoveKey<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.RemoveKey(key));
        }

        /// <summary>
        /// Removes the specified keys from the cache.
        /// Any keys not contained in the cache are ignored.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Edit(updater => updater.RemoveKeys(keys));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all right values and combining any matching left values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, TRight, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.RightJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all right values and combining any matching left values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TRightKey, Optional<TLeft>, TRight, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, , taking all right values and combining any matching left values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which.</typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) =&gt; new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return left.RightJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector,, taking all right values and combining any matching left values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left data source.</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left data source.</typeparam>
        /// <typeparam name="TRight">The object type of the right data source.</typeparam>
        /// <typeparam name="TRightKey">The key type of the right data source.</typeparam>
        /// <typeparam name="TDestination">The resulting object which. </typeparam>
        /// <param name="left">The left data source.</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right data source.</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right).</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
            where TLeftKey : notnull
            where TRightKey : notnull
        {
            if (left is null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right is null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightKeySelector is null)
            {
                throw new ArgumentNullException(nameof(rightKeySelector));
            }

            if (resultSelector is null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            return new RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Defer the subscription until loaded and skip initial change set.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.DeferUntilLoaded().Skip(1);
        }

        /// <summary>
        /// Sorts using the specified comparer.
        /// Returns the underlying ChangeSet as as per the system conventions.
        /// The resulting change set also exposes a sorted key value collection of of the underlying cached data.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations.</param>
        /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sort).</param>
        /// <returns>An observable which emits change sets.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// comparer.
        /// </exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (comparer is null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            return new Sort<TObject, TKey>(source, comparer, sortOptimisations, resetThreshold: resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparerObservable">The comparer observable.</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (comparerObservable is null)
            {
                throw new ArgumentNullException(nameof(comparerObservable));
            }

            return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resetThreshold: resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparerObservable">The comparer observable.</param>
        /// <param name="resorter">Signal to instruct the algorithm to re-sort the entire data set.</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (comparerObservable is null)
            {
                throw new ArgumentNullException(nameof(comparerObservable));
            }

            return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resorter, resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer to sort on.</param>
        /// <param name="resorter">Signal to instruct the algorithm to re-sort the entire data set.</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (resorter is null)
            {
                throw new ArgumentNullException(nameof(resorter));
            }

            return new Sort<TObject, TKey>(source, comparer, sortOptimisations, null, resorter, resetThreshold).Run();
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.StartWith(ChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits sorted change sets.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.StartWith(SortedChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits virtual change sets.</returns>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits paged change sets.</returns>
        public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.StartWith(PagedChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits group change sets.</returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
            where TKey : notnull
            where TGroupKey : notnull
        {
            return source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits immutable group change sets.</returns>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
            where TKey : notnull
            where TGroupKey : notnull
        {
            return source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty change set to the source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source read only collection.</param>
        /// <returns>A read only collection.</returns>
        public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source)
        {
            return source.StartWith(ReadOnlyCollectionLight<T>.Empty);
        }

        /// <summary>
        /// The equivalent of rx StartsWith operator, but wraps the item in a change where reason is ChangeReason.Add.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item)
            where TObject : IKey<TKey>
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.StartWithItem(item, item.Key);
        }

        /// <summary>
        /// The equivalent of rx StartWith operator, but wraps the item in a change where reason is ChangeReason.Add.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var change = new Change<TObject, TKey>(ChangeReason.Add, key, item);
            return source.StartWith(new ChangeSet<TObject, TKey> { change });
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and un-subscribes when it is removed.  All items will be unsubscribed when the stream is disposed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subscription function.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory.</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and un-subscribes when it is removed.
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (subscriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(subscriptionFactory));
            }

            return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubscribes when it is removed.  All items will be unsubscribed when the stream is disposed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subscription function.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory.</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubscribes when it is removed.
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (subscriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(subscriptionFactory));
            }

            return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
        }

        /// <summary>
        /// Suppress refresh notifications.
        /// </summary>
        /// <typeparam name="TObject">The object of the change set.</typeparam>
        /// <typeparam name="TKey">The key of the change set.</typeparam>
        /// <param name="source">The source observable change set.</param>
        /// <returns>An observable which emits change sets.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> SuppressRefresh<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.WhereReasonsAreNot(ChangeReason.Refresh);
        }

        /// <summary>
        /// Transforms an observable sequence of observable caches into a single sequence
        /// producing values only from the most recent observable sequence.
        /// Each time a new inner observable sequence is received, unsubscribe from the
        /// previous inner observable sequence and clear the existing result set.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservableCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Select(cache => cache.Connect()).Switch();
        }

        /// <summary>
        /// Transforms an observable sequence of observable changes sets into an observable sequence
        /// producing values only from the most recent observable sequence.
        /// Each time a new inner observable sequence is received, unsubscribe from the
        /// previous inner observable sequence and clear the existing result set.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return new Switch<TObject, TKey>(sources).Run();
        }

        /// <summary>
        /// Converts the change set into a fully formed collection. Each change in the source results in a new collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits the read only collection.</returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items));
        }

        /// <summary>
        /// Converts the observable to an observable change set.
        /// Change set observes observable change events.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit.</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns>An observable which will emit changes.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this IObservable<TObject> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter = null, int limitSizeTo = -1, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return new ToObservableChangeSet<TObject, TKey>(source, keySelector, expireAfter, limitSizeTo, scheduler).Run();
        }

        /// <summary>
        /// Converts the observable to an observable change set.
        /// Change set observes observable change events.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit.</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns>An observable change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter = null, int limitSizeTo = -1, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return new ToObservableChangeSet<TObject, TKey>(source, keySelector, expireAfter, limitSizeTo, scheduler).Run();
        }

        /// <summary>
        /// Limits the size of the result set to the specified number.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns>An observable which will emit virtual change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero.</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, int size)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
            }

            return new Virtualise<TObject, TKey>(source, Observable.Return(new VirtualRequest(0, size))).Run();
        }

        /// <summary>
        /// Limits the size of the result set to the specified number, ordering by the comparer.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="size">The size.</param>
        /// <returns>An observable which will emit virtual change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero.</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, int size)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (comparer is null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
            }

            return source.Sort(comparer).Top(size);
        }

        /// <summary>
        /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TSortKey">The sort key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sort">The sort function.</param>
        /// <param name="sortOrder">The sort order. Defaults to ascending.</param>
        /// <returns>An observable which emits the read only collection.</returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TKey, TSortKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
            where TKey : notnull
            where TSortKey : notnull
        {
            return source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.Items.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.Items.OrderByDescending(sort)));
        }

        /// <summary>
        /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The sort comparer.</param>
        /// <returns>An observable which emits the read only collection.</returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer)
            where TKey : notnull
        {
            return source.QueryWhenChanged(
                query =>
                    {
                        var items = query.Items.AsList();
                        items.Sort(comparer);
                        return new ReadOnlyCollectionLight<TObject>(items);
                    });
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.Transform((current, _, _) => transformFactory(current), transformOnRefresh);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, bool transformOnRefresh)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.Transform((current, _, key) => transformFactory(current, key), transformOnRefresh);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, bool transformOnRefresh)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return new Transform<TDestination, TSource, TKey>(source, transformFactory, transformOnRefresh: transformOnRefresh).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.Transform((current, _, _) => transformFactory(current), forceTransform?.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.Transform((current, _, key) => transformFactory(current, key), forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (forceTransform is not null)
            {
                return new TransformWithForcedTransform<TDestination, TSource, TKey>(source, transformFactory, forceTransform).Run();
            }

            return new Transform<TDestination, TSource, TKey>(source, transformFactory).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            return source.Transform((cur, _, _) => transformFactory(cur), forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>#
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (forceTransform is null)
            {
                throw new ArgumentNullException(nameof(forceTransform));
            }

            return source.Transform((cur, _, key) => transformFactory(cur, key), forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>#
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (forceTransform is null)
            {
                throw new ArgumentNullException(nameof(forceTransform));
            }

            return source.Transform(transformFactory, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.TransformAsync((current, _, _) => transformFactory(current), forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.TransformAsync((current, _, key) => transformFactory(current, key), forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, forceTransform).Run();
        }

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <returns>An observable with the transformed change set.</returns>
        /// <param name="source">The source.</param>
        /// <param name="manySelector">Will select a enumerable of values.</param>
        /// <param name="keySelector">The key selector which must be unique across all.</param>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
            where TSourceKey : notnull
            where TDestinationKey : notnull
        {
            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();
        }

        /// <summary>
        /// Flatten the nested observable collection, and subsequently observe observable collection changes.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <returns>An observable with the transformed change set.</returns>
        /// <param name="source">The source.</param>
        /// <param name="manySelector">Will select a enumerable of values.</param>
        /// <param name="keySelector">The key selector which must be unique across all.</param>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
            where TSourceKey : notnull
            where TDestinationKey : notnull
        {
            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();
        }

        /// <summary>
        /// Flatten the nested observable collection, and subsequently observe observable collection changes.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <returns>An observable with the transformed change set.</returns>
        /// <param name="source">The source.</param>
        /// <param name="manySelector">Will select a enumerable of values.</param>
        /// <param name="keySelector">The key selector which must be unique across all.</param>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
            where TSourceKey : notnull
            where TDestinationKey : notnull
        {
            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            return source.TransformSafe((current, _, _) => transformFactory(current), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            return source.TransformSafe((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            if (forceTransform is not null)
            {
                return new TransformWithForcedTransform<TDestination, TSource, TKey>(source, transformFactory, forceTransform, errorHandler).Run();
            }

            return new Transform<TDestination, TSource, TKey>(source, transformFactory, errorHandler).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            return source.TransformSafe((cur, _, _) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>#
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (forceTransform is null)
            {
                throw new ArgumentNullException(nameof(forceTransform));
            }

            return source.TransformSafe((cur, _, key) => transformFactory(cur, key), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items.</param>#
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (forceTransform is null)
            {
                throw new ArgumentNullException(nameof(forceTransform));
            }

            return source.TransformSafe(transformFactory, errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, forceTransform).Run();
        }

        /// <summary>
        /// Transforms the object to a fully recursive tree, create a hierarchy based on the pivot function.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pivotOn">The pivot on.</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns>An observable which will emit change sets.</returns>
        public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn, IObservable<Func<Node<TObject, TKey>, bool>>? predicateChanged = null)
            where TKey : notnull
            where TObject : class
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (pivotOn is null)
            {
                throw new ArgumentNullException(nameof(pivotOn));
            }

            return new TreeBuilder<TObject, TKey>(source, pivotOn, predicateChanged).Run();
        }

        /// <summary>
        /// Converts moves changes to remove + add.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>the same SortedChangeSets, except all moves are replaced with remove + add.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> TreatMovesAsRemoveAdd<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IEnumerable<Change<TObject, TKey>> ReplaceMoves(IChangeSet<TObject, TKey> items)
            {
                foreach (var change in items)
                {
                    if (change.Reason == ChangeReason.Moved)
                    {
                        yield return new Change<TObject, TKey>(ChangeReason.Remove, change.Key, change.Current, change.PreviousIndex);

                        yield return new Change<TObject, TKey>(ChangeReason.Add, change.Key, change.Current, change.CurrentIndex);
                    }
                    else
                    {
                        yield return change;
                    }
                }
            }

            return source.Select(changes => new SortedChangeSet<TObject, TKey>(changes.SortedItems, ReplaceMoves(changes)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        ///
        /// i) The cache changes
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns>An observable which boolean values indicating if true.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
            where TKey : notnull
        {
            return source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        ///
        /// i) The cache changes
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns>An observable which boolean values indicating if true.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
            where TKey : notnull
        {
            return source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns>An observable which boolean values indicating if true.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition.
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
            where TKey : notnull
        {
            return source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns>An observable which boolean values indicating if true.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition.
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (observableSelector is null)
            {
                throw new ArgumentNullException(nameof(observableSelector));
            }

            if (equalityCondition is null)
            {
                throw new ArgumentNullException(nameof(equalityCondition));
            }

            return source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }

        /// <summary>
        /// Updates the index for an object which implements IIndexAware.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable which emits the sorted change set.</returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TKey : notnull
            where TObject : IIndexAware
        {
            return source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));
        }

        /// <summary>
        /// Virtualises the underlying data from the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="virtualRequests">The virirtualising requests.</param>
        /// <returns>An observable which will emit virtual change sets.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IVirtualRequest> virtualRequests)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (virtualRequests is null)
            {
                throw new ArgumentNullException(nameof(virtualRequests));
            }

            return new Virtualise<TObject, TKey>(source, virtualRequests).Run();
        }

        /// <summary>
        /// Returns an observable of any updates which match the specified key,  proceeded with the initial cache state.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns>An observable which emits the change.</returns>
        public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
        }

        /// <summary>
        /// Watches updates for a single value matching the specified key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns>An observable which emits the object value.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Watch(key).Select(u => u.Current);
        }

        /// <summary>
        /// Watches updates for a single value matching the specified key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns>An observable which emits the object value.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Watch(key).Select(u => u.Current);
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes.</param>
        /// <returns>An observable which emits the object which has had a property changed.</returns>
        public static IObservable<TObject?> WhenAnyPropertyChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns>An observable which emits a property when it has changed.</returns>
        public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertyAccessor is null)
            {
                throw new ArgumentNullException(nameof(propertyAccessor));
            }

            return source.MergeMany(t => t.WhenPropertyChanged(propertyAccessor, notifyOnInitialValue));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns>An observable which emits a value when it has changed.</returns>
        public static IObservable<TValue?> WhenValueChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TKey : notnull
            where TObject : INotifyPropertyChanged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (propertyAccessor is null)
            {
                throw new ArgumentNullException(nameof(propertyAccessor));
            }

            return source.MergeMany(t => t.WhenChanged(propertyAccessor, notifyOnInitialValue));
        }

        /// <summary>
        /// Includes changes for the specified reasons only.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns>An observable which emits a change set with items matching the reasons.</returns>
        /// <exception cref="System.ArgumentNullException">reasons.</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAre<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
            where TKey : notnull
        {
            if (reasons is null)
            {
                throw new ArgumentNullException(nameof(reasons));
            }

            if (reasons.Length == 0)
            {
                throw new ArgumentException("Must select at least one reason");
            }

            var hashed = new HashSet<ChangeReason>(reasons);

            return source.Select(updates => new ChangeSet<TObject, TKey>(updates.Where(u => hashed.Contains(u.Reason)))).NotEmpty();
        }

        /// <summary>
        /// Excludes updates for the specified reasons.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns>An observable which emits a change set with items not matching the reasons.</returns>
        /// <exception cref="System.ArgumentNullException">reasons.</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
            where TKey : notnull
        {
            if (reasons is null)
            {
                throw new ArgumentNullException(nameof(reasons));
            }

            if (reasons.Length == 0)
            {
                throw new ArgumentException("Must select at least one reason");
            }

            var hashed = new HashSet<ChangeReason>(reasons);

            return source.Select(updates => new ChangeSet<TObject, TKey>(updates.Where(u => !hashed.Contains(u.Reason)))).NotEmpty();
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections.
        /// Items which are only in one of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (others is null || others.Length == 0)
            {
                throw new ArgumentNullException(nameof(others));
            }

            return source.Combine(CombineOperator.Xor, others);
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections.
        /// Items which are only in one of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others.
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are only in one of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits a change set.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits a change set.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>An observable which emits a change set.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed.</param>
        /// <param name="interval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of enumerable of the key values which has been removed.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector.</exception>
        internal static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ForExpiry<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval, IScheduler scheduler)
            where TKey : notnull
        {
            return new TimeExpirer<TObject, TKey>(source, timeSelector, interval, scheduler).ForExpiry();
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> source, CombineOperator type)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                        var subscriber = connections.Combine(type).SubscribeSafe(observer);
                        return new CompositeDisposable(connections, subscriber);
                    });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> source, CombineOperator type)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                        var subscriber = connections.Combine(type).SubscribeSafe(observer);
                        return new CompositeDisposable(connections, subscriber);
                    });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new DynamicCombiner<TObject, TKey>(source, type).Run();
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources, CombineOperator type)
            where TKey : notnull
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        void UpdateAction(IChangeSet<TObject, TKey> updates)
                        {
                            try
                            {
                                observer.OnNext(updates);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                            }
                        }

                        IDisposable subscriber = Disposable.Empty;
                        try
                        {
                            var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                            subscriber = combiner.Subscribe(sources.ToArray());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            observer.OnCompleted();
                        }

                        return subscriber;
                    });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, CombineOperator type, params IObservable<IChangeSet<TObject, TKey>>[] combineTarget)
            where TKey : notnull
        {
            if (combineTarget is null)
            {
                throw new ArgumentNullException(nameof(combineTarget));
            }

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        void UpdateAction(IChangeSet<TObject, TKey> updates)
                        {
                            try
                            {
                                observer.OnNext(updates);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                observer.OnCompleted();
                            }
                        }

                        IDisposable subscriber = Disposable.Empty;
                        try
                        {
                            var list = combineTarget.ToList();
                            list.Insert(0, source);

                            var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                            subscriber = combiner.Subscribe(list.ToArray());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            observer.OnCompleted();
                        }

                        return subscriber;
                    });
        }

        private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Unit>? source)
            where TKey : notnull
        {
            return source?.Select(
                _ =>
                    {
                        bool Transformer(TSource item, TKey key) => true;
                        return (Func<TSource, TKey, bool>)Transformer;
                    });
        }

        private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Func<TSource, bool>>? source)
            where TKey : notnull
        {
            return source?.Select(
                condition =>
                    {
                        bool Transformer(TSource item, TKey key) => condition(item);
                        return (Func<TSource, TKey, bool>)Transformer;
                    });
        }

        private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
            where TKey : notnull
        {
            return new TrueFor<TObject, TKey, TValue>(source, observableSelector, collectionMatcher).Run();
        }
    }
}