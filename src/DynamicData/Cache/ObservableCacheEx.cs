// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;
using DynamicData.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    private const int DefaultSortResetThreshold = 100;
    private const bool DefaultResortOnSourceRefresh = true;

    /// <summary>
    /// Inject side effects into the stream using the specified adaptor.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="adaptor">The adaptor.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// destination.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// destination.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return source.Do(adaptor.Adapt);
    }

    /// <summary>
    /// Adds or updates the cache with the specified item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items));
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
    /// <param name="equalityComparer">The equality comparer used to determine whether a new item is the same as an existing cached item.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items, equalityComparer));
    }

    /// <summary>
    /// Adds or updates the cache with the specified item / key pair.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source cache.</param>
    /// <param name="item">The item to add or update.</param>
    /// <param name="key">The key to add or update.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void AddOrUpdate<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        item.ThrowArgumentNullExceptionIfNull(nameof(item));

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
    /// <exception cref="ArgumentNullException">source or others.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return others is null || others.Length == 0
            ? throw new ArgumentNullException(nameof(others))
            : source.Combine(CombineOperator.And, others);
    }

    /// <summary>
    /// Applied a logical And operator between the collections i.e items which are in all of the sources are included.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    /// <summary>
    /// Converts the source to an read only observable cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable cache.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, bool applyLocking = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            (t, _) =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenAnyPropertyChanged();
                }

                return t.WhenAnyPropertyChanged().Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
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
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            (t, _) =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenPropertyChanged(propertyAccessor, false);
                }

                return t.WhenPropertyChanged(propertyAccessor, false).Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
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
        where TObject : notnull
        where TKey : notnull => source.AutoRefreshOnObservable((t, _) => reevaluator(t), changeSetBuffer, scheduler);

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// scheduler.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan timeSpan, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Buffer(timeSpan, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult();
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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, scheduler);

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, scheduler: scheduler).Run();

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IObservable<Unit>? timer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, timer, scheduler).Run();

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="refreshThreshold">The number of changes before a reset notification is triggered.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, int refreshThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = refreshThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = refreshThreshold };

        return source?.Bind(destination, new ObservableCollectionAdaptor<TObject, TKey>(options)) ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="options"> The binding options.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source?.Bind(destination, new ObservableCollectionAdaptor<TObject, TKey>(options)) ?? throw new ArgumentNullException(nameof(source));
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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, IObservableCollectionAdaptor<TObject, TKey> updater)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));
        updater.ThrowArgumentNullExceptionIfNull(nameof(updater));

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
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="options"> The binding options.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        return source.Bind(target, new ObservableCollectionAdaptor<TObject, TKey>(options));
    }

    /// <summary>
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="resetThreshold">The number of changes before a reset notification is triggered.</param>
    /// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates.  NB: Some platforms to not support replace notifications for binding.</param>
    /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold, bool useReplaceForUpdates = BindingOptions.DefaultUseReplaceForUpdates, IObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (adaptor is not null)
        {
            var target = new ObservableCollectionExtended<TObject>();
            readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
            return source.Bind(target, adaptor);
        }

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold && useReplaceForUpdates == BindingOptions.DefaultUseReplaceForUpdates
            ? defaults
            : defaults with { ResetThreshold = resetThreshold, UseReplaceForUpdates = useReplaceForUpdates };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Bind(destination, DynamicDataOptions.Binding);
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="options"> The binding options.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        var updater = new SortedObservableCollectionAdaptor<TObject, TKey>(options);
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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, ISortedObservableCollectionAdaptor<TObject, TKey> updater)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));
        updater.ThrowArgumentNullExceptionIfNull(nameof(updater));

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
    /// <param name="options"> The binding options.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var target = new ObservableCollectionExtended<TObject>();
        var result = new ReadOnlyObservableCollection<TObject>(target);
        var updater = new SortedObservableCollectionAdaptor<TObject, TKey>(options);
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
    /// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates.  NB: Some platforms to not support replace notifications for binding.</param>
    /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold, bool useReplaceForUpdates = BindingOptions.DefaultUseReplaceForUpdates, ISortedObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold && useReplaceForUpdates == BindingOptions.DefaultUseReplaceForUpdates
            ? defaults
            : defaults with { ResetThreshold = resetThreshold, UseReplaceForUpdates = useReplaceForUpdates };

        adaptor ??= new SortedObservableCollectionAdaptor<TObject, TKey>(options);

        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        return source.Bind(target, adaptor);
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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

        return source.Adapt(new BindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
    }

    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="TObject">The object type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="bindingList">The target binding list.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

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
        where TObject : notnull
        where TKey : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

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
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TSourceKey, TObject, TDestinationKey> keySelector)
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <summary>
    /// Clears all data.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Clear<TObject, TKey>(this LockFreeObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        target.ThrowArgumentNullExceptionIfNull(nameof(target));

        return source.Do(
            changes =>
            {
                foreach (var item in changes.ToConcreteType())
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
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// <para>Disposes each item when no longer required.</para>
    /// <para>
    /// Individual items are disposed after removal or replacement changes have been sent downstream.
    /// All items previously-published on the stream are disposed after the stream finalizes.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>A continuation of the original stream.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<TObject, TKey>(source).Run();
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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        allItems.ThrowArgumentNullExceptionIfNull(nameof(allItems));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void EditDiff<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> allItems, Func<TObject, TObject, bool> areItemsEqual)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        allItems.ThrowArgumentNullExceptionIfNull(nameof(allItems));
        areItemsEqual.ThrowArgumentNullExceptionIfNull(nameof(areItemsEqual));

        var editDiff = new EditDiff<TObject, TKey>(source, areItemsEqual);
        editDiff.Edit(allItems);
    }

    /// <summary>
    /// Converts an Observable of Enumerable to an Observable ChangeSet that updates when the enumerables changes.  Counterpart operator to <see cref="ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">Key Selection Function for the ChangeSet.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to use for comparing values.</param>
    /// <returns>An observable cache.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSet<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an Observable Optional to an Observable ChangeSet that adds/removes/updates as the optional changes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">Key Selection Function for the ChangeSet.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to use for comparing values.</param>
    /// <returns>An observable changeset.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSetOptional<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Ensures there are no duplicated keys in the observable changeset.
    /// </summary>
    /// <param name="source"> The source change set.</param>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <returns>A changeset which guarantees a key is only present at most once in the changeset.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueKeys<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new UniquenessEnforcer<TObject, TKey>(source).Run();
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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// timeSelector.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector);

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// timeSelector.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                IScheduler scheduler)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            scheduler: scheduler);

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// timeSelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval);

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// timeSelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval,
                IScheduler scheduler)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// timeSelector.</exception>
    public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(
                this ISourceCache<TObject, TKey> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval = null,
                IScheduler? scheduler = null)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForSource<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Filters the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="suppressEmptyChangeSets">By default empty changeset notifications are suppressed for performance reasons.  Set to false to publish empty changesets.  Doing so can be useful for monitoring loading status.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new StaticFilter<TObject, TKey>(source, filter, suppressEmptyChangeSets).Run();
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
    /// <param name="suppressEmptyChangeSets">By default empty changeset notifications are suppressed for performance reasons.  Set to false to publish empty changesets.  Doing so can be useful for monitoring loading status.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicateChanged.ThrowArgumentNullExceptionIfNull(nameof(predicateChanged));

        return source.Filter(predicateChanged, Observable.Empty<Unit>(), suppressEmptyChangeSets);
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
    /// <param name="suppressEmptyChangeSets">By default empty changeset notifications are suppressed for performance reasons.  Set to false to publish empty changesets.  Doing so can be useful for monitoring loading status.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Unit> reapplyFilter, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reapplyFilter.ThrowArgumentNullExceptionIfNull(nameof(reapplyFilter));

        return source.Filter(Observable.Empty<Func<TObject, bool>>(), reapplyFilter, suppressEmptyChangeSets);
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
    /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
    /// <param name="suppressEmptyChangeSets">By default empty changeset notifications are suppressed for performance reasons.  Set to false to publish empty changesets.  Doing so can be useful for monitoring loading status.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, IObservable<Unit> reapplyFilter, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicateChanged.ThrowArgumentNullExceptionIfNull(nameof(predicateChanged));
        reapplyFilter.ThrowArgumentNullExceptionIfNull(nameof(reapplyFilter));

        return new DynamicFilter<TObject, TKey>(source, predicateChanged, reapplyFilter, suppressEmptyChangeSets).Run();
    }

    /// <summary>
    /// Creates a filtered stream, optimized for stateless/deterministic filtering of immutable items.
    /// </summary>
    /// <typeparam name="TObject">The type of collection items to be filtered.</typeparam>
    /// <typeparam name="TKey">The type of the key values of each collection item.</typeparam>
    /// <param name="source">The source stream of collection items to be filtered.</param>
    /// <param name="predicate">The filtering predicate to be applied to each item.</param>
    /// <param name="suppressEmptyChangeSets">A flag indicating whether the created stream should emit empty changesets. Empty changesets are suppressed by default, for performance. Set to ensure that a downstream changeset occurs for every upstream changeset.</param>
    /// <returns>A stream of collection changesets where upstream collection items are filtered by the given predicate.</returns>
    /// <remarks>
    /// <para>The goal of this operator is to optimize a common use-case of reactive programming, where data values flowing through a stream are immutable, and state changes are distributed by publishing new immutable items as replacements, instead of mutating the items directly.</para>
    /// <para>In addition to assuming that all collection items are immutable, this operator also assumes that the given filter predicate is deterministic, such that the result it returns will always be the same each time a specific input is passed to it. In other words, the predicate itself also contains no mutable state.</para>
    /// <para>Under these assumptions, this operator can bypass the need to keep track of every collection item that passes through it, which the normal <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> operator must do, in order to re-evaluate the filtering status of items, during a refresh operation.</para>
    /// <para>Consider using this operator when the following are true:</para>
    /// <list type="bullet">
    /// <item><description>Your collection items are immutable, and changes are published by replacing entire items</description></item>
    /// <item><description>Your filtering logic does not change over the lifetime of the stream, only the items do</description></item>
    /// <item><description>Your filtering predicate runs quickly, and does not heavily allocate memory</description></item>
    /// </list>
    /// <para>Note that, because filtering is purely deterministic, Refresh operations are transparently ignored by this operator.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterImmutable<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, bool> predicate,
            bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterImmutable<TObject, TKey>(
                predicate: predicate,
                source: source,
                suppressEmptyChangeSets: suppressEmptyChangeSets)
            .Run();
    }

    /// <summary>
    /// Filters the stream of changes according to an Observable bool that is created for each item using the specified factory function.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="filterFactory">Factory function used to create the observable bool that controls whether that given item passes the filter or not.</param>
    /// <param name="buffer">Optional time to buffer changes from the observable bools.</param>
    /// <param name="scheduler">Optional scheduler to use when buffering the changes.</param>
    /// <returns>An observable changeset that only contains items whose corresponding observable bool has emitted true as its most recent value.</returns>
    /// <exception cref="ArgumentNullException">One of the given parameters was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return new FilterOnObservable<TObject, TKey>(source, filterFactory, buffer, scheduler).Run();
    }

    /// <summary>
    /// Filters the stream of changes according to an Observable bool that is created for each item using the specified factory function.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="filterFactory">Factory function used to create the observable bool that controls whether that given item passes the filter or not.</param>
    /// <param name="buffer">Optional time to buffer changes from the observable bools.</param>
    /// <param name="scheduler">Optional scheduler to use when buffering the changes.</param>
    /// <returns>An observable changeset that only contains items whose corresponding observable bool has emitted true as its most recent value.</returns>
    /// <exception cref="ArgumentNullException">One of the given parameters was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return source.FilterOnObservable((obj, _) => filterFactory(obj), buffer, scheduler);
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
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));
        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterOnProperty<TObject, TKey, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Ensure that finally is always called. Thanks to Lee Campbell for this.
    /// </summary>
    /// <typeparam name="T">The type contained within the observables.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="finallyAction">The finally action.</param>
    /// <returns>An observable which has always a finally action applied.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    [Obsolete("This can cause unhandled exception issues so do not use")]
    public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        finallyAction.ThrowArgumentNullExceptionIfNull(nameof(finallyAction));

        return new FinallySafe<T>(source, finallyAction).Run();
    }

    /// <summary>
    /// Flattens an update collection to it's individual items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits change set values on a flatten result.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        action.ThrowArgumentNullExceptionIfNull(nameof(action));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelector.ThrowArgumentNullExceptionIfNull(nameof(groupSelector));
        resultGroupSource.ThrowArgumentNullExceptionIfNull(nameof(resultGroupSource));

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
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelectorKey
    /// or
    /// groupController.
    /// </exception>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));
        regrouper.ThrowArgumentNullExceptionIfNull(nameof(regrouper));

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    /// <summary>
    /// Groups the source on the value returned by the latest value from the group selector factory observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupSelectorKeyObservable">The group selector key observable.</param>
    /// <param name="regrouper">Fires when the current Grouping Selector needs to re-evaluate all the items in the cache.</param>
    /// <returns>An observable which will emit group change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelectorKey
    /// or
    /// groupController.
    /// </exception>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));
        regrouper.ThrowArgumentNullExceptionIfNull(nameof(regrouper));

        return new GroupOnDynamic<TObject, TKey, TGroupKey>(source, groupSelectorKeyObservable, regrouper).Run();
    }

    /// <summary>
    /// Groups the source on the value returned by the latest value from the group selector factory observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupSelectorKeyObservable">The group selector key observable.</param>
    /// <param name="regrouper">Fires when the current Grouping Selector needs to re-evaluate all the items in the cache.</param>
    /// <returns>An observable which will emit group change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelectorKey
    /// or
    /// groupController.
    /// </exception>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));

        return source.Group(groupSelectorKeyObservable.Select(AdaptSelector<TObject, TKey, TGroupKey>), regrouper);
    }

    /// <summary>
    /// Groups the source by the latest value from their observable created by the given factory.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupObservableSelector">The group selector key.</param>
    /// <returns>An observable which will emit group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnObservable<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> groupObservableSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupObservableSelector.ThrowArgumentNullExceptionIfNull(nameof(groupObservableSelector));

        return new GroupOnObservable<TObject, TKey, TGroupKey>(source, groupObservableSelector).Run();
    }

    /// <summary>
    /// Groups the source by the latest value from their observable created by the given factory.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupObservableSelector">The group selector key.</param>
    /// <returns>An observable which will emit group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnObservable<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TGroupKey>> groupObservableSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        groupObservableSelector.ThrowArgumentNullExceptionIfNull(nameof(groupObservableSelector));

        return source.GroupOnObservable(AdaptSelector<TObject, TKey, IObservable<TGroupKey>>(groupObservableSelector));
    }

    /// <summary>
    /// <para>Groups the source using the property specified by the property selector. Groups are re-applied when the property value changed.</para>
    /// <para>When there are likely to be a large number of group property changes specify a throttle to improve performance.</para>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnProperty<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// <para>Groups the source using the property specified by the property selector. Each update produces immutable grouping. Groups are re-applied when the property value changed.</para>
    /// <para>When there are likely to be a large number of group property changes specify a throttle to improve performance.</para>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelectorKey
    /// or
    /// groupController.
    /// </exception>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

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
        where TObject : notnull
        where TKey : notnull => source.IgnoreUpdateWhen((c, p) => ReferenceEquals(c, p));

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
        where TObject : notnull
        where TKey : notnull => source.Select(
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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        includeFunction.ThrowArgumentNullExceptionIfNull(nameof(includeFunction));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TKey : notnull => source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Refresh).ForEach(u => u.Current.Evaluate()));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentException">size cannot be zero.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> LimitSizeTo<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, int size)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentException">Size limit must be greater than zero.</exception>
    public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source, int sizeLimit, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("Size limit must be greater than zero", nameof(sizeLimit));
        }

        return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(
            observer =>
            {
                long orderItemWasAdded = -1;
                var sizeLimiter = new SizeLimiter<TObject, TKey>(sizeLimit);

                return source.Connect().Finally(observer.OnCompleted).ObserveOn(scheduler ?? GlobalConfig.DefaultScheduler).Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded))).Select(sizeLimiter.CloneAndReturnExpiredOnly).Where(expired => expired.Length != 0).Subscribe(
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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// observableSelector.</exception>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// observableSelector.</exception>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges both observable changesets into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="other">The Other Observable ChangeSet.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges both observable changesets into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="other">The Other Observable ChangeSet.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new[] { source, other }.MergeChangeSets(comparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges both observable changesets into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="other">The Other Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges both observable changesets into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="other">The Other Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new[] { source, other }.MergeChangeSets(equalityComparer, comparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges the source changeset and the collection of other changesets together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="others">The Other Observable ChangeSets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges the source changeset and the collection of other changesets together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="others">The Other Observable ChangeSets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(comparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges the source changeset and the collection of other changesets together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="others">The Other Observable ChangeSets.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges the source changeset and the collection of other changesets together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="others">The Other Observable ChangeSets.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, comparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer, completable, scheduler).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events that correctly handles multiple Keys.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple changesets.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer, completable, scheduler).Run();
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TObject, TKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), comparer);
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TObject, TKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TObject, TKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), equalityComparer, comparer);
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TObject, TKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="resortOnSourceRefresh">Optional boolean to indicate whether or not a refresh event in the parent stream should re-evaluate item priorities.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="resortOnSourceRefresh">Optional boolean to indicate whether or not a refresh event in the parent stream should re-evaluate item priorities.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="resortOnSourceRefresh">Optional boolean to indicate whether or not a refresh event in the parent stream should re-evaluate item priorities.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// Overload of <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> that
    /// will handle key collisions by using an <see cref="IComparer{T}"/> instance that operates on the sources, so that the values from the preferred source take precedent over other values with the same.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="sourceComparer"><see cref="IComparer{T}"/> instance to determine which source elements child to use when two sources provide a child element with the same key.</param>
    /// <param name="resortOnSourceRefresh">Optional boolean to indicate whether or not a refresh event in the parent stream should re-evaluate item priorities.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="childComparer">Optional fallback <see cref="IComparer{T}"/> instance to determine which child element to emit if the sources compare to be the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        sourceComparer.ThrowArgumentNullExceptionIfNull(nameof(sourceComparer));

        return new MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, sourceComparer, equalityComparer, childComparer, resortOnSourceRefresh).Run();
    }

    /// <summary>
    /// Merges the List ChangeSets derived from items in a Cache ChangeSet into a single observable list changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyListChangeSets<TObject, TKey, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// Merges the List ChangeSets derived from items in a Cache ChangeSet into a single observable list changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        return source.MergeManyChangeSets((obj, _) => observableSelector(obj), equalityComparer);
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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// observableSelector.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// observableSelector.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Monitors the status of a stream.
    /// </summary>
    /// <typeparam name="T">The type of the source observable.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which monitors the status of the observable.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source) => new StatusMonitor<T>(source).Run();

    /// <summary>
    /// Suppresses updates which are empty.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits change set values when not empty.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(changes => changes.Count != 0);
    }

    /// <summary>
    /// Filters an observable changeset so that it only includes items that are of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the objects that are allowed to pass the filter.</typeparam>
    /// <param name="source">The source observable changeset of <typeparamref name="TObject"/> instances.</param>
    /// <param name="suppressEmptyChangeSets">Indicates whether or not to suppress changesets that end up being empty after the conversion.</param>
    /// <returns>An observable changeset of <typeparamref name="TDestination"/> where each item was either converted from <paramref name="source"/> or filtered out.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>Combines a filter and a transform into a single step that does not use an intermediate cache.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> OfType<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new OfType<TObject, TKey, TDestination>(source, suppressEmptyChangeSets).Run();
    }

    /// <summary>
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="addAction">The add action that takes the new value and the associated key.</param>
    /// <returns>An observable which emits a change set with items being added.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> addAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));

        return source.OnChangeAction(ChangeReason.Add, addAction);
    }

    /// <summary>
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="addAction">The add action that takes the new value.</param>
    /// <returns>An observable which emits a change set with items being added.</returns>
    /// <remarks>Overload for <see cref="OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/> with a callback that doesn't use a key.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> addAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemAdded((obj, _) => addAction(obj));

    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="refreshAction">The refresh action that takes the refreshed value and the key.</param>
    /// <returns>An observable which emits a change set with items being added.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> refreshAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        refreshAction.ThrowArgumentNullExceptionIfNull(nameof(refreshAction));

        return source.OnChangeAction(ChangeReason.Refresh, refreshAction);
    }

    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="refreshAction">The refresh action that takes the refreshed value.</param>
    /// <returns>An observable which emits a change set with items being added.</returns>
    /// <remarks>Overload for <see cref="OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/> with a callback that doesn't use a key.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> refreshAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRefreshed((obj, _) => refreshAction(obj));

    /// <summary>
    /// Callback for each item/key as and when it is being removed from the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="removeAction">The remove action that takes the removed value and the key.</param>
    /// <param name="invokeOnUnsubscribe"> Should the remove action be invoked when the subscription is disposed.</param>
    /// <returns>An observable which emits a change set with items being removed.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// removeAction.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));

        if (invokeOnUnsubscribe)
        {
            return new OnBeingRemoved<TObject, TKey>(source, removeAction).Run();
        }

        return source.OnChangeAction(ChangeReason.Remove, removeAction);
    }

    /// <summary>
    /// Callback for each item as and when it is being removed from the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="removeAction">The remove action that takes the removed value.</param>
    /// <param name="invokeOnUnsubscribe"> Should the remove action be invoked when the subscription is disposed.</param>
    /// <returns>An observable which emits a change set with items being removed.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// removeAction.
    /// </exception>
    /// <remarks>Overload for <see cref="OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey}, bool)"/> with a callback that doesn't use the key.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRemoved((obj, _) => removeAction(obj), invokeOnUnsubscribe);

    /// <summary>
    /// Callback when an item has been updated eg. (current, previous)=>{}.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="updateAction">The update action that takes current value, previous value, and the key.</param>
    /// <returns>An observable which emits a change set with items being updated.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject, TKey> updateAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.OnChangeAction(static change => change.Reason == ChangeReason.Update, change => updateAction(change.Current, change.Previous.Value, change.Key));
    }

    /// <summary>
    /// Callback when an item has been updated eg. (current, previous)=>{}.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="updateAction">The update action that takes the current value and previous value.</param>
    /// <returns>An observable which emits a change set with items being updated.</returns>
    /// <remarks>Overload for <see cref="OnItemUpdated{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TObject, TKey})"/> with a callback that doesn't use the key.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemUpdated((cur, prev, _) => updateAction(cur, prev));

    /// <summary>
    /// Apply a logical Or operator between the collections i.e items which are in any of the sources are included.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="others">The others.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Populate a cache from an observable stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="observable">The observable.</param>
    /// <returns>A disposable which will unsubscribe from the source.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// keySelector.
    /// </exception>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// keySelector.
    /// </exception>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// destination.
    /// </exception>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// destination.</exception>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// resultSelector.
    /// </exception>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the query.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        itemChangedTrigger.ThrowArgumentNullExceptionIfNull(nameof(itemChangedTrigger));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// Signal observers to re-evaluate the specified item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(item));
    }

    /// <summary>
    /// Signal observers to re-evaluate the specified items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(items));
    }

    /// <summary>
    /// Signal observers to re-evaluate the all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh());
    }

    /// <summary>
    /// <para>Removes the specified item from the cache.</para>
    /// <para>If the item is not contained in the cache then the operation does nothing.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// <para>Removes the specified items from the cache.</para>
    /// <para>Any items not contained in the cache are ignored.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(items));
    }

    /// <summary>
    /// <para>Removes the specified keys from the cache.</para>
    /// <para>Any keys not contained in the cache are ignored.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keys">The keys.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// <para>Removes the specified keys from the cache.</para>
    /// <para>Any keys not contained in the cache are ignored.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keys">The keys.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void RemoveKey<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <summary>
    /// Defer the subscription until loaded and skip initial change set.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Sorts using the specified comparer.
    /// Returns the underlying ChangeSet as per the system conventions.
    /// The resulting change set also exposes a sorted key value collection of the underlying cached data.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer.</param>
    /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations.</param>
    /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sort).</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// comparer.
    /// </exception>
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

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
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparerObservable.ThrowArgumentNullExceptionIfNull(nameof(comparerObservable));

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
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparerObservable.ThrowArgumentNullExceptionIfNull(nameof(comparerObservable));

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
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resorter.ThrowArgumentNullExceptionIfNull(nameof(resorter));

        return new Sort<TObject, TKey>(source, comparer, sortOptimisations, null, resorter, resetThreshold).Run();
    }

    /// <summary>
    /// Sorts a sequence by selected property.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="sortOrder">The sort order. Defaults to ascending.</param>
    /// <param name="sortOptimisations">The sort optimisations.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> SortBy<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, IComparable> expression,
        SortDirection sortOrder = SortDirection.Ascending,
        SortOptimisations sortOptimisations = SortOptimisations.None,
        int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source = source ?? throw new ArgumentNullException(nameof(source));
        expression = expression ?? throw new ArgumentNullException(nameof(expression));

        return source.Sort(
            sortOrder switch
            {
                SortDirection.Descending => SortExpressionComparer<TObject>.Descending(expression),
                _ => SortExpressionComparer<TObject>.Ascending(expression),
            },
            sortOptimisations,
            resetThreshold);
    }

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(ChangeSet<TObject, TKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits sorted change sets.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(SortedChangeSet<TObject, TKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits virtual change sets.</returns>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits paged change sets.</returns>
    public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(PagedChangeSet<TObject, TKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits immutable group change sets.</returns>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source read only collection.</param>
    /// <returns>A read only collection.</returns>
    public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source) => source.StartWith(ReadOnlyCollectionLight<T>.Empty);

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// subscriptionFactory.</exception>
    /// <remarks>
    /// Subscribes to each item when it is added or updates and un-subscribes when it is removed.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// subscriptionFactory.</exception>
    /// <remarks>
    /// Subscribes to each item when it is added or updates and unsubscribes when it is removed.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

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
        where TObject : notnull
        where TKey : notnull => source.WhereReasonsAreNot(ChangeReason.Refresh);

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull => source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this IObservable<TObject> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter = null, int limitSizeTo = -1, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter = null, int limitSizeTo = -1, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new ToObservableChangeSet<TObject, TKey>(source, keySelector, expireAfter, limitSizeTo, scheduler).Run();
    }

    /// <summary>
    /// Converts an observable change set into an observable optional that emits the value for the given key.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key value.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new ToObservableOptional<TObject, TKey>(source, key, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an observable cache into an observable optional that emits the value for the given key.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key value.</param>
    /// <param name="initialOptionalWhenMissing">Indicates if an initial Optional None should be emitted if the value doesn't exist.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, bool initialOptionalWhenMissing, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (initialOptionalWhenMissing)
        {
            var seenValue = false;
            var locker = new object();

            var optional = source.ToObservableOptional(key, equalityComparer).Synchronize(locker).Do(_ => seenValue = true);
            var missing = Observable.Return(Optional.None<TObject>()).Synchronize(locker).Where(_ => !seenValue);

            return optional.Merge(missing);
        }

        return source.ToObservableOptional(key, equalityComparer);
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
        where TObject : notnull
        where TKey : notnull
        where TSortKey : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.Items.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.Items.OrderByDescending(sort)));

    /// <summary>
    /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The sort comparer.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.Items.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.Transform((cur, _, _) => transformFactory(cur), forceTransform.ForForced<TSource, TKey>());

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, forceTransform).Run();
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="options">The transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, _) => transformFactory(current), options);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="options">The transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, key) => transformFactory(current, key), options);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="options">The transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function, with optimizations for stateless/deterministic transformation of immutable items.
    /// </summary>
    /// <typeparam name="TDestination">The type of collection items produced by the transformation.</typeparam>
    /// <typeparam name="TSource">The type of collection items to be transformed.</typeparam>
    /// <typeparam name="TKey">The type of the key values of each collection item.</typeparam>
    /// <param name="source">The source stream of collection items to be transformed.</param>
    /// <param name="transformFactory">The transformation to be applied to each item.</param>
    /// <returns>A stream of collection changesets where upstream collection items are transformed by the given factory function.</returns>
    /// <remarks>
    /// <para>The goal of this operator is to optimize a common use-case of reactive programming, where data values flowing through a stream are immutable, and state changes are distributed by publishing new immutable items as replacements, instead of mutating the items directly.</para>
    /// <para>In addition to assuming that all collection items are immutable, this operator also assumes that the given transformation function is deterministic, such that the result it returns will always be equivalent each time a specific input is passed to it. In other words, the transformation itself also contains no mutable state.</para>
    /// <para>Under these assumptions, this operator can bypass the need to keep track of every collection item that passes through it, which the normal <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, bool)"/> operator must do, in order to re-evaluate transformations during a refresh operation.</para>
    /// <para>Consider using this operator when the following are true:</para>
    /// <list type="bullet">
    /// <item><description>Your collection items are immutable, and changes are published by replacing entire items</description></item>
    /// <item><description>Your transformation logic does not change over the lifetime of the stream, only the items do</description></item>
    /// <item><description>Your transformation function runs quickly, and does not heavily allocate memory</description></item>
    /// </list>
    /// <para>Note that, because transformation is purely deterministic, Refresh operations are transparently ignored by this operator.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformImmutable<TDestination, TSource, TKey>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformImmutable<TDestination, TSource, TKey>(
                source: source,
                transformFactory: transformFactory)
            .Run();
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
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

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
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

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
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Flatten the nested observable cache, and subsequently observe observable cache changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Will select an observable cache of values.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable changeset with the transformed values.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="IEnumerable{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable changeset with the transformed values.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> into an <see cref="IEnumerable{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, ObservableCollection{TDestination}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TCollection">The type of an observable collection of <typeparamref name="TDestination"/>.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="ObservableCollection{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, ObservableCollection{TDestination}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TCollection">The type of an observable collection of <typeparamref name="TDestination"/>.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> into an <see cref="ObservableCollection{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IObservableCache{TDestination, TDestinationKey}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="IObservableCache{TObject, TKey}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector), equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IObservableCache{TDestination, TDestinationKey}}, Func{TDestination, TDestinationKey})"/> except that it allows the tranformation function to be an async method.  Also supports comparison and sorting to prioritize values the same destination key returned from multiple sources.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="IObservableCache{TObject, TKey}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), equalityComparer, comparer);

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable changeset with the transformed values.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="IEnumerable{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable changeset with the transformed values.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> into an <see cref="IEnumerable{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey, TCollection}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{TCollection}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TCollection">The type of an observable collection of <typeparamref name="TDestination"/>.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="ObservableCollection{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey, TCollection}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, Task{TCollection}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TCollection">The type of an observable collection of <typeparamref name="TDestination"/>.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> into an <see cref="ObservableCollection{T}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IObservableCache{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> and <typeparamref name="TSourceKey"/> into an <see cref="IObservableCache{TObject, TKey}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTranformer(manySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Extension method similar to <see cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IObservableCache{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> except it accepts an error handler so that failed transformations are not fatal errors.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <returns>An observable with the transformed change set.</returns>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">Async function to transform a <typeparamref name="TSource"/> into an <see cref="IObservableCache{TObject, TKey}"/> of <typeparamref name="TDestination"/>.</param>
    /// <param name="errorHandler">Callback function for handling an errors.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <remarks>Because the transformations are asynchronous, unlike TransformMany, each sub-collection could be emitted via a separate changeset.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Transforms each item in the ChangeSet into an Observable that provides the value for the Resulting ChangeSet.
    /// </summary>
    /// <typeparam name="TSource">The type of the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination changeset.</typeparam>
    /// <param name="source">The source changeset observable.</param>
    /// <param name="transformFactory">Factory function to create the Observable that will provide the values in the result changeset from the given object in the source changeset.</param>
    /// <returns>
    /// A changeset whose value for a given key is the latest value emitted from the transformed Observable and will update to future values from that observable.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformOnObservable<TSource, TKey, TDestination>(source, transformFactory).Run();
    }

    /// <summary>
    /// Transforms each item in the ChangeSet into an Observable that provides the value for the Resulting ChangeSet.
    /// </summary>
    /// <typeparam name="TSource">The type of the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination changeset.</typeparam>
    /// <param name="source">The source changeset observable.</param>
    /// <param name="transformFactory">Factory function to create the Observable that will provide the values in the result changeset from the given object in the source changeset.</param>
    /// <returns>
    /// A changeset whose value for a given key is the latest value emitted from the transformed Observable and will update to future values from that observable.
    /// </returns>
    /// <exception cref="ArgumentNullException">source or transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformOnObservable((obj, _) => transformFactory(obj));
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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));
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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.TransformSafe((cur, _, _) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, forceTransform).Run();
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
    /// <param name="options">Additional transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, options);
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
    /// <param name="options">Additional transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, options);
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
    /// <param name="options">Additional transform options.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
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
        where TObject : class
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pivotOn.ThrowArgumentNullExceptionIfNull(nameof(pivotOn));

        return new TreeBuilder<TObject, TKey>(source, pivotOn, predicateChanged).Run();
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function and when an update is received, allows the preservation of the previous instance.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="updateAction">Apply changes to the original. Example (previousTransformedItem, newOriginalItem) => previousTransformedItem.Value = newOriginalItem.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, false);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function and when an update is received, allows the preservation of the previous instance.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="updateAction">Apply changes to the original. Example (previousTransformedItem, newOriginalItem) => previousTransformedItem.Value = newOriginalItem.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, transformOnRefresh: transformOnRefresh).Run();
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function and when an update is received, allows the preservation of the previous instance.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="updateAction">Apply changes to the original. Example (previousTransformedItem, newOriginalItem) => previousTransformedItem.Value = newOriginalItem.</param>
    /// <param name="errorHandler">The error handler.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, errorHandler, false);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function and when an update is received, allows the preservation of the previous instance.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="updateAction">Apply changes to the original. Example (previousTransformedItem, newOriginalItem) => previousTransformedItem.Value = newOriginalItem.</param>
    /// <param name="errorHandler">The error handler.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, errorHandler, transformOnRefresh).Run();
    }

    /// <summary>
    /// Converts moves changes to remove + add.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>the same SortedChangeSets, except all moves are replaced with remove + add.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> TreatMovesAsRemoveAdd<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        static IEnumerable<Change<TObject, TKey>> ReplaceMoves(IChangeSet<TObject, TKey> items)
        {
            foreach (var change in items.ToConcreteType())
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
    /// <para>
    /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
    /// the equality condition. The observable is re-evaluated whenever.
    /// </para>
    /// <para>
    /// i) The cache changes
    /// or ii) The inner observable changes.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="observableSelector">Selector which returns the target observable.</param>
    /// <param name="equalityCondition">The equality condition.</param>
    /// <returns>An observable which boolean values indicating if true.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));

    /// <summary>
    /// <para>
    /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
    /// the equality condition. The observable is re-evaluated whenever.
    /// </para>
    /// <para>
    /// i) The cache changes
    /// or ii) The inner observable changes.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="observableSelector">Selector which returns the target observable.</param>
    /// <param name="equalityCondition">The equality condition.</param>
    /// <returns>An observable which boolean values indicating if true.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// observableSelector
    /// or
    /// equalityCondition.
    /// </exception>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// observableSelector
    /// or
    /// equalityCondition.
    /// </exception>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        equalityCondition.ThrowArgumentNullExceptionIfNull(nameof(equalityCondition));

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
        where TObject : IIndexAware
        where TKey : notnull => source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));

    /// <summary>
    /// Returns an observable of any updates which match the specified key,  proceeded with the initial cache state.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <returns>An observable which emits the change.</returns>
    public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <returns>An observable which emits a property when it has changed.</returns>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

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
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <returns>An observable which emits a value when it has changed.</returns>
    public static IObservable<TValue?> WhenValueChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

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
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAre<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

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
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        where TObject : notnull
        where TKey : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Xor);
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DynamicCombiner<TObject, TKey>(source, type).Run();
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

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

                var subscriber = Disposable.Empty;
                try
                {
                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. sources]);
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
        where TObject : notnull
        where TKey : notnull
    {
        combineTarget.ThrowArgumentNullExceptionIfNull(nameof(combineTarget));

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

                var subscriber = Disposable.Empty;
                try
                {
                    var list = combineTarget.ToList();
                    list.Insert(0, source);

                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. list]);
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
        where TKey : notnull => source?.Select(
            _ =>
            {
                static bool Transformer(TSource item, TKey key) => true;
                return (Func<TSource, TKey, bool>)Transformer;
            });

    private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Func<TSource, bool>>? source)
        where TKey : notnull => source?.Select(
            condition =>
            {
                bool Transformer(TSource item, TKey key) => condition(item);
                return (Func<TSource, TKey, bool>)Transformer;
            });

    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Predicate<Change<TObject, TKey>> predicate, Action<Change<TObject, TKey>> changeAction)
        where TObject : notnull
        where TKey : notnull
    {
        return source.Do(changes =>
        {
            foreach (var change in changes.ToConcreteType())
            {
                if (!predicate(change))
                {
                    continue;
                }

                changeAction(change);
            }
        });
    }

    // TODO: Apply the Adapter to more places
    private static Func<TObject, TKey, TResult> AdaptSelector<TObject, TKey, TResult>(Func<TObject, TResult> other)
        where TObject : notnull
        where TKey : notnull
        where TResult : notnull => (obj, _) => other(obj);

    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ChangeReason reason, Action<TObject, TKey> action)
        where TObject : notnull
        where TKey : notnull
        => source.OnChangeAction(change => change.Reason == reason, change => action(change.Current, change.Key));

    private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => new TrueFor<TObject, TKey, TValue>(source, observableSelector, collectionMatcher).Run();

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTranformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).AsObservableChangeSet(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTranformer<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).ToObservableChangeSet<TCollection, TDestination>().AddKey(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTranformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).Connect();
}
