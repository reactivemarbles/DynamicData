// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
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
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

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
    /// Injects a side effect into the changeset stream by calling <paramref name="adaptor"/>.<see cref="IChangeSetAdaptor{TObject, TKey}.Adapt(IChangeSet{TObject, TKey})"/>
    /// for every changeset, then forwarding it downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="adaptor"><see cref="IChangeSetAdaptor{TObject, TKey}"/> the adaptor whose <c>Adapt</c> method is called for each changeset.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, after the adaptor has processed each one.</returns>
    /// <remarks>
    /// <para>
    /// This is a thin wrapper around Rx's <c>Do</c> operator. The adaptor receives each changeset
    /// as a side effect; the changeset itself is forwarded downstream unmodified.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Passed to the adaptor, then forwarded.</description></item>
    /// <item><term>Update</term><description>Passed to the adaptor, then forwarded.</description></item>
    /// <item><term>Remove</term><description>Passed to the adaptor, then forwarded.</description></item>
    /// <item><term>Refresh</term><description>Passed to the adaptor, then forwarded.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer. The adaptor is not called.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="adaptor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Adapt{TObject, TKey}(IObservable{ISortedChangeSet{TObject, TKey}}, ISortedChangeSetAdaptor{TObject, TKey})"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservableCollection{TObject}, IObservableCollectionAdaptor{TObject, TKey})"/>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return source.Do(adaptor.Adapt);
    }

    /// <inheritdoc cref="Adapt{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IChangeSetAdaptor{TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source sorted changeset stream.</param>
    /// <param name="adaptor"><see cref="IChangeSetAdaptor{TObject, TKey}"/> the sorted adaptor whose <c>Adapt</c> method is called for each sorted changeset.</param>
    /// <remarks>This overload operates on <see cref="ISortedChangeSet{TObject, TKey}"/>. Delegates to Rx's <c>Do</c> operator.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return source.Do(adaptor.Adapt);
    }

    /// <summary>
    /// Adds or updates the cache with the specified item, producing a changeset with a single <b>Add</b>
    /// (if the key is new) or <b>Update</b> (if the key already exists).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item mutation inside <see cref="ISourceCache{TObject,TKey}.Edit"/>.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Produced when the key does not already exist in the cache.</description></item>
    /// <item><term>Update</term><description>Produced when the key already exists. The previous value is included in the changeset.</description></item>
    /// <item><term>Remove</term><description>Not produced by this method.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this method.</description></item>
    /// <item><term>OnError</term><description>Not applicable (synchronous mutation).</description></item>
    /// <item><term>OnCompleted</term><description>Not applicable (synchronous mutation).</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="EditDiff{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject}, IEqualityComparer{TObject})"/>
    /// <seealso cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(item));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> the equality comparer used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>This overload uses <paramref name="equalityComparer"/> to suppress no-op updates when the new value equals the existing one.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(item, equalityComparer));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="items"><see cref="IEnumerable{{T}}"/> the items to add or update.</param>
    /// <remarks>Batch overload. All items are added/updated inside a single <see cref="ISourceCache{TObject,TKey}.Edit"/> call, producing one changeset.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="items"><see cref="IEnumerable{{T}}"/> the items to add or update.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> the equality comparer used to determine whether a new item is the same as an existing cached item. When equal, the update is skipped.</param>
    /// <remarks>Batch overload with equality comparison. All items are added/updated inside a single <see cref="ISourceCache{TObject,TKey}.Edit"/> call.</remarks>
    public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.AddOrUpdate(items, equalityComparer));
    }

    /// <inheritdoc cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <param name="source"><see cref="IIntermediateCache{{TObject, TKey}}"/> the source intermediate cache.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to add or update.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to associate with the item.</param>
    /// <remarks>This overload operates on <see cref="IIntermediateCache{TObject, TKey}"/>, which requires an explicit key parameter.</remarks>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="others"><see cref="IEnumerable{T}"/> the others.</param>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    /// <summary>
    /// Wraps an <see cref="IObservableCache{TObject, TKey}"/> in a read-only facade, hiding the mutable API.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache to wrap.</param>
    /// <returns>A read-only <see cref="IObservableCache{TObject, TKey}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableCache<TObject, TKey>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a queryable, read-only <see cref="IObservableCache{TObject, TKey}"/>.
    /// The cache subscribes to the source on first access and maintains a live snapshot of all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="applyLocking">If <see langword="true"/> (default), all cache operations are synchronized. Set to <see langword="false"/> when the caller guarantees single-threaded access.</param>
    /// <returns>A read-only observable cache that reflects the current state of the pipeline.</returns>
    /// <remarks>
    /// <para>
    /// Disposing the returned cache unsubscribes from the source stream. The cache's <c>Connect()</c>
    /// method provides a changeset stream of its own, which re-emits the current state on each new subscriber.
    /// </para>
    /// <para>When <paramref name="applyLocking"/> is <see langword="false"/>, a <see cref="LockFreeObservableCache{TObject, TKey}"/> is used internally.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservableCache{TObject, TKey})"/>
    /// <seealso cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
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

    #if SUPPORTS_ASYNC_DISPOSABLE
    /// <summary>
    /// <para>
    /// Disposes items implementing <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> when they are removed or replaced,
    /// and disposes all tracked items when the stream completes, errors, or the subscription is disposed.
    /// </para>
    /// <para>
    /// Individual items are disposed <b>after</b> the changeset has been forwarded downstream, so downstream operators
    /// see the removal before disposal occurs. Items implementing neither disposal interface are ignored.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of items in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="disposalsCompletedAccessor">
    /// <para>
    /// Invoked once per subscription, providing an <see cref="IObservable{Unit}"/> that signals when all
    /// <see cref="IAsyncDisposable.DisposeAsync()"/> calls have finished. The signal emits a single value
    /// and then completes.
    /// </para>
    /// <para>
    /// This is delivered on a separate channel from the main changeset stream so it can be observed even
    /// if the source stream errors.
    /// </para>
    /// </param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Tracks the item. No disposal.</description></item>
    ///   <item><term>Update</term><description>Disposes the <b>previous</b> value (if it differs by reference from the current). Tracks the new value.</description></item>
    ///   <item><term>Remove</term><description>Disposes the removed item.</description></item>
    ///   <item><term>Refresh</term><description>Passed through. No disposal.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// On stream completion, error, or subscription disposal, all items still in the cache are disposed.
    /// <see cref="IDisposable"/> items are disposed synchronously; <see cref="IAsyncDisposable"/> items
    /// are dispatched via the <paramref name="disposalsCompletedAccessor"/> signal.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="disposalsCompletedAccessor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DisposeMany{TObject,TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> AsyncDisposeMany<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Action<IObservable<Unit>> disposalsCompletedAccessor)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.AsyncDisposeMany<TObject, TKey>.Create(
            source: source,
            disposalsCompletedAccessor: disposalsCompletedAccessor);
    #endif

    /// <summary>
    /// Automatically refresh downstream operators when any properties change.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable.</param>
    /// <param name="changeSetBuffer">A optional <see cref="TimeSpan"/> batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">A optional <see cref="TimeSpan"/> when observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{{TDelegate}}"/> that specify a property to observe changes. When it changes a Refresh is invoked.</param>
    /// <param name="changeSetBuffer">A optional <see cref="TimeSpan"/> batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">A optional <see cref="TimeSpan"/> when observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable change set.</param>
    /// <param name="reevaluator"><see cref="Func{{T, TResult}}"/> an observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">A optional <see cref="TimeSpan"/> batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable change set.</param>
    /// <param name="reevaluator"><see cref="Func{{T, TResult}}"/> an observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">A optional <see cref="TimeSpan"/> batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> Per-item observable errors are silently ignored (not forwarded to the downstream observer). Only source stream errors propagate.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return new AutoRefresh<TObject, TKey, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }

    /// <summary>
    /// Collects changesets emitted within a time window and merges them into a single changeset.
    /// Uses Rx's <c>Buffer</c> operator followed by <see cref="FlattenBufferResult{TObject, TKey}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="timeSpan"><see cref="TimeSpan"/> the time window for batching.</param>
    /// <param name="scheduler">The scheduler for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits merged changesets, one per time window.</returns>
    /// <remarks>
    /// <para>
    /// All changesets received during the time window are concatenated into a single changeset.
    /// This is useful for reducing UI update frequency when the source emits many rapid changes.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Buffered and included in the merged changeset at the end of the time window.</description></item>
    /// <item><term>Update</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Remove</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Refresh</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Any remaining buffered changes are flushed, then completion is forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The merged changeset may contain contradictory changes (e.g., Add then Remove for the same key). Downstream operators handle this correctly, but raw inspection of the changeset may be surprising.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="BufferInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan timeSpan, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Buffer(timeSpan, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult();
    }

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload delegates to the primary overload with <c>initialPauseState: false</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, scheduler);

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload delegates to the primary overload with default <c>initialPauseState: false</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, scheduler: scheduler).Run();

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload omits <c>initialPauseState</c> (defaults to <see langword="false"/>) but accepts a timeout.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

    /// <summary>
    /// Conditionally buffers changesets while a pause signal is active, then flushes all buffered
    /// changes as a single merged changeset when the signal resumes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{T}"/> that when <see langword="true"/>, buffering begins. When <see langword="false"/>, the buffer is flushed.</param>
    /// <param name="initialPauseState">If <see langword="true"/>, starts in a paused (buffering) state.</param>
    /// <param name="timeOut">A <see cref="TimeSpan"/> that maximum time the buffer stays open. When elapsed, the buffer is flushed regardless of pause state.</param>
    /// <param name="scheduler"><see cref="IScheduler"/> the scheduler for timeout timing.</param>
    /// <returns>An observable that emits changesets, buffered or passthrough depending on pause state.</returns>
    /// <remarks>
    /// <para>
    /// While paused, incoming changesets are accumulated. On resume (or timeout), all buffered changesets
    /// are merged into a single changeset and emitted. While not paused, changesets pass through immediately.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Update</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Remove</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Refresh</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer. Buffered data is lost.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded. Any remaining buffered data is flushed before completion.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If the source completes while paused, buffered data IS flushed before OnCompleted. However, if the source errors while paused, buffered data is lost.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pauseIfTrueSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Batch{TObject, TKey}"/>
    /// <seealso cref="BufferInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, timeOut, initialPauseState, scheduler: scheduler).Run();
    }

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{{T}}"/> that when <see langword="true"/>, buffering begins. When <see langword="false"/>, the buffer is flushed.</param>
    /// <param name="initialPauseState">If <see langword="true"/>, starts in a paused (buffering) state.</param>
    /// <param name="timer">An optional <see cref="IObservable{{T}}"/> an observable timer. The buffer is flushed each time the timer produces a value, and buffering ceases when it completes.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
    /// <remarks>This overload accepts an explicit timer observable instead of a <see cref="TimeSpan"/> timeout.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IObservable<Unit>? timer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, timer, scheduler).Run();

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
    /// <param name="options">A <see cref="BindingOptions"/> that  The binding options.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
    /// <param name="updater"><see cref="IObservable{{T}}"/> the updater.</param>
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
                var locker = InternalEx.NewLock();
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="readOnlyObservableCollection"><see cref="ReadOnlyObservableCollection{T}"/> the resulting read only observable collection.</param>
    /// <param name="options">A <see cref="BindingOptions"/> that  The binding options.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="readOnlyObservableCollection"><see cref="ReadOnlyObservableCollection{T}"/> the resulting read only observable collection.</param>
    /// <param name="resetThreshold">The number of changes before a reset notification is triggered.</param>
    /// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates.  NB: Some platforms to not support replace notifications for binding.</param>
    /// <param name="adaptor">An optional <see cref="IObservable{{T}}"/> specify an adaptor to change the algorithm to update the target collection.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
    /// <param name="options">A <see cref="BindingOptions"/> that  The binding options.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IObservable{{T}}"/> the destination.</param>
    /// <param name="updater"><see cref="ISortedObservableCollectionAdaptor{TObject, TKey}"/> the updater.</param>
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
                var locker = InternalEx.NewLock();
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="readOnlyObservableCollection"><see cref="ReadOnlyObservableCollection{T}"/> the resulting read only observable collection.</param>
    /// <param name="options">A <see cref="BindingOptions"/> that  The binding options.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="readOnlyObservableCollection"><see cref="ReadOnlyObservableCollection{T}"/> the resulting read only observable collection.</param>
    /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection.</param>
    /// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates.  NB: Some platforms to not support replace notifications for binding.</param>
    /// <param name="adaptor">An <see cref="IChangeSetAdaptor{TObject, TKey}"/> that specify an adaptor to change the algorithm to update the target collection.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="bindingList"><see cref="BindingList{T}"/> the target binding list.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="bindingList"><see cref="BindingList{T}"/> the target binding list.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

        return source.Adapt(new SortedBindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
    }

#endif

    /// <summary>
    /// Buffers the initial burst of changesets for the specified duration, merges them into a single
    /// changeset, then passes all subsequent changesets through without buffering.
    /// </summary>
    /// <typeparam name="TObject">The object type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source change set.</param>
    /// <param name="initialBuffer"><see cref="TimeSpan"/> the time window to buffer, measured from when the first changeset arrives.</param>
    /// <param name="scheduler">The scheduler for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits one merged changeset for the initial burst, then passthrough for the rest.</returns>
    /// <remarks>
    /// <para>
    /// Useful for aggregating the initial snapshot (which may arrive as many small changesets) into a
    /// single changeset for efficient downstream processing, while leaving subsequent live updates untouched.
    /// </para>
    /// <para>Internally uses <see cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>, Rx <c>Buffer</c>, and <see cref="FlattenBufferResult{TObject, TKey}"/>.</para>
    /// </remarks>
    /// <seealso cref="Batch{TObject, TKey}"/>
    /// <seealso cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> BufferInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

    /// <summary>
    /// Casts each item in the changeset to a new type using the provided converter function.
    /// Equivalent to <see cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/>
    /// but named for discoverability when a simple type cast or conversion is needed.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="converter"><see cref="Func{{T, TResult}}"/> the conversion function applied to each item.</param>
    /// <returns>An observable changeset of converted items.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="converter"/> and emits an <b>Add</b> with the converted item.</description></item>
    /// <item><term>Update</term><description>Calls <paramref name="converter"/> on the new value and emits an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Emits a <b>Remove</b>. The converter is not called.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b>. The converter is not called.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OfType{TObject, TKey, TDestination}"/>
    public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new Cast<TSource, TKey, TDestination>(source, converter).Run();
    }

    /// <summary>
    /// Re-keys each item in the changeset by applying <paramref name="keySelector"/> to the current item.
    /// The original change reason is preserved; only the key is remapped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="keySelector"><see cref="Func{{T, TResult}}"/> a function that computes the destination key from the item, e.g. <c>(item) =&gt; item.NewId</c>.</param>
    /// <returns>An observable changeset with items re-keyed using <paramref name="keySelector"/>.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description><paramref name="keySelector"/> is called on the item. An <b>Add</b> is emitted with the destination key.</description></item>
    /// <item><term>Update</term><description><paramref name="keySelector"/> is called on the current item. An <b>Update</b> is emitted with the destination key. If the key selector produces a different destination key for the updated value than it did for the original value, downstream consumers will see an <b>Update</b> for a key that may not match the original <b>Add</b>.</description></item>
    /// <item><term>Remove</term><description><paramref name="keySelector"/> is called on the item. A <b>Remove</b> is emitted with the destination key.</description></item>
    /// <item><term>Refresh</term><description><paramref name="keySelector"/> is called on the item. A <b>Refresh</b> is emitted with the destination key.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/>
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

    /// <inheritdoc cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    /// <remarks>
    /// This overload also provides the source key to <paramref name="keySelector"/>,
    /// allowing the destination key to be derived from both the item and its original key.
    /// </remarks>
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
    /// Removes all items from the cache, producing a changeset with a <b>Remove</b> for every item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to clear.</param>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Not produced by this operation.</description></item>
    /// <item><term>Update</term><description>Not produced by this operation.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted for every item currently in the cache.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operation.</description></item>
    /// <item><term>OnError</term><description>Not applicable (synchronous mutation method).</description></item>
    /// <item><term>OnCompleted</term><description>Not applicable (synchronous mutation method).</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <inheritdoc cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Clear());
    }

    /// <inheritdoc cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    public static void Clear<TObject, TKey>(this LockFreeObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        source.Edit(updater => updater.Clear());
    }

    /// <summary>
    /// Applies each change from the source changeset to the specified <paramref name="target"/> collection as a side effect.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="target"><see cref="ICollection{{T}}"/> the target collection to which changes are applied.</param>
    /// <returns>An observable that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The item is added to <paramref name="target"/>. Forwarded as <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>The previous item is removed from <paramref name="target"/> and the current item is added. Forwarded as <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>The item is removed from <paramref name="target"/>. Forwarded as <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Ignored (<see cref="ICollection{T}"/> has no concept of refresh). Forwarded as <b>Refresh</b>.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
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
    /// Obsolete: use <see cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/> instead.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="conversionFactory"><see cref="Func{{T, TResult}}"/> the conversion factory.</param>
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
    /// Suppresses all emissions until the first non-empty changeset arrives, then replays that changeset and all subsequent ones.
    /// If the source never produces a non-empty changeset, the stream waits indefinitely.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable that begins emitting changesets once the first non-empty changeset is received.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Forwarded as <b>Add</b> once the initial non-empty changeset has been received.</description></item>
    /// <item><term>Update</term><description>Forwarded as <b>Update</b> once loaded.</description></item>
    /// <item><term>Remove</term><description>Forwarded as <b>Remove</b> once loaded.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> once loaded.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Blocks indefinitely if the cache or stream never receives any data. Ensure the source will eventually emit at least one changeset.</para>
    /// </remarks>
    /// <seealso cref="SkipInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// <para>
    /// Disposes items implementing <see cref="IDisposable"/> when they are removed or replaced,
    /// and disposes all tracked items when the stream completes, errors, or the subscription is disposed.
    /// </para>
    /// <para>
    /// Individual items are disposed <b>after</b> the changeset has been forwarded downstream, so downstream operators
    /// see the removal before disposal occurs. Items that do not implement <see cref="IDisposable"/> are ignored.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Tracks the item. No disposal.</description></item>
    ///   <item><term>Update</term><description>Disposes the <b>previous</b> value (if it differs by reference from the current). Tracks the new value.</description></item>
    ///   <item><term>Remove</term><description>Disposes the removed item.</description></item>
    ///   <item><term>Refresh</term><description>Passed through. No disposal.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// On stream completion, error, or subscription disposal, all remaining tracked items are disposed.
    /// All disposal is synchronous via <see cref="IDisposable.Dispose()"/>.
    /// For items that implement <see cref="IAsyncDisposable"/>, use <see cref="AsyncDisposeMany{TObject,TKey}"/> instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsyncDisposeMany{TObject,TKey}"/>
    /// <seealso cref="SubscribeMany{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="valueSelector"><see cref="Func{{T, TResult}}"/> the value selector.</param>
    /// <returns>An observable which will emit distinct change sets.</returns>
    /// <remarks>
    /// Due to it's nature only adds or removes can be returned.
    /// <para><b>Worth noting:</b> Reference counting assumes value equality is transitive. Mutable value objects with inconsistent <c>Equals</c> implementations can corrupt ref counts.</para>
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

    /// <inheritdoc cref="EditDiff{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject}, Func{TObject, TObject, bool})"/>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to diff against.</param>
    /// <param name="allItems"><see cref="IEnumerable{{T}}"/> the complete snapshot of items to diff against the cache.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> used to determine whether a new item is the same as an existing cached item.</param>
    /// <remarks>
    /// This overload uses an <see cref="IEqualityComparer{T}"/> instead of a <see cref="Func{T, T, TResult}"/> delegate
    /// to determine item equality.
    /// </remarks>
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
    /// Diffs a complete snapshot of items against the current cache contents, producing the minimal set of
    /// Add, Update, and Remove changes needed to bring the cache in sync with the snapshot.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to diff against.</param>
    /// <param name="allItems"><see cref="IEnumerable{{T}}"/> the complete snapshot of desired items.</param>
    /// <param name="areItemsEqual"><see cref="Func{{T, TResult}}"/> a function that returns <see langword="true"/> when the current and previous items are considered equal, e.g. <c>(current, previous) =&gt; current.Version == previous.Version</c>.</param>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Items in <paramref name="allItems"/> whose key is not in the cache produce an <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Items present in both <paramref name="allItems"/> and the cache that differ (per <paramref name="areItemsEqual"/>) produce an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Items in the cache whose key is not in <paramref name="allItems"/> produce a <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operation.</description></item>
    /// <item><term>OnError</term><description>Not applicable (synchronous mutation method).</description></item>
    /// <item><term>OnCompleted</term><description>Not applicable (synchronous mutation method).</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="allItems"/>, or <paramref name="areItemsEqual"/> is <see langword="null"/>.</exception>
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
    /// Converts an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> into a changeset stream by diffing each
    /// emission against the previous one. Each emission replaces the entire dataset.
    /// Counterpart to <see cref="ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable of item snapshots.</param>
    /// <param name="keySelector"><see cref="Func{{T, TResult}}"/> a function to extract the unique key from each item.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> for comparing items. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable changeset representing the incremental differences between successive snapshots.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Items in the new snapshot whose key was not in the previous snapshot produce an <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Items present in both snapshots that differ (per <paramref name="equalityComparer"/>) produce an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Items in the previous snapshot whose key is absent from the new snapshot produce a <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operator.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSet<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an <see cref="IObservable{T}"/> of <see cref="Optional{T}"/> into a changeset stream that tracks
    /// a single item: <c>Some</c> produces an <b>Add</b> or <b>Update</b>, and <c>None</c> produces a <b>Remove</b>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable of optional values.</param>
    /// <param name="keySelector"><see cref="Func{{T, TResult}}"/> a function to extract the unique key from each item.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> for comparing items. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable changeset tracking the single optional item.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emitted when the source produces <c>Some(value)</c> and no item was previously tracked.</description></item>
    /// <item><term>Update</term><description>Emitted when the source produces <c>Some(value)</c> and an item was already tracked with a different value (per <paramref name="equalityComparer"/>).</description></item>
    /// <item><term>Remove</term><description>Emitted when the source produces <c>None</c> and an item was previously tracked.</description></item>
    /// <item><term>Refresh</term><description>Not produced by this operator.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> EditDiff<TObject, TKey>(this IObservable<Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return new EditDiffChangeSetOptional<TObject, TKey>(source, keySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Validates that each changeset contains no duplicate keys.
    /// If duplicates are detected, an <see cref="InvalidOperationException"/> is emitted via <c>OnError</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream to validate.</param>
    /// <returns>A changeset stream guaranteed to contain unique keys per changeset.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Forwarded as <b>Add</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Update</term><description>Forwarded as <b>Update</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Remove</term><description>Forwarded as <b>Remove</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the key is unique within the changeset.</description></item>
    /// <item><term>OnError</term><description>Forwarded. Also emitted with <see cref="InvalidOperationException"/> if duplicate keys are detected in a changeset.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="others"><see cref="IEnumerable{T}"/> the others.</param>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the sources.</param>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Schedules automatic removal of items after the timeout returned by <paramref name="timeSelector"/>.
    /// If <paramref name="timeSelector"/> returns <see langword="null"/>, the item never expires.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="timeSelector">A optional <see cref="Func{{T, TResult}}"/> a function returning the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <returns>An observable changeset that includes timer-driven <b>Remove</b> changes for expired items.</returns>
    /// <remarks>
    /// <para>When a timer fires, a <b>Remove</b> is emitted for the expired item.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Schedules a removal timer based on <paramref name="timeSelector"/>. Forwarded as <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Resets the removal timer for the item. Forwarded as <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Cancels the removal timer. Forwarded as <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b>. No timer change.</description></item>
    /// <item><term>OnError</term><description>Forwarded. All pending timers are cancelled.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded. All pending timers are cancelled.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> A <see langword="null"/> return from <paramref name="timeSelector"/> means "never expire". <b>Update</b> changes reset the expiration timer.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="timeSelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector);

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="timeSelector">A optional <see cref="Func{{T, TResult}}"/> a function returning the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="scheduler"><see cref="IScheduler"/> the scheduler used to schedule expiration timers.</param>
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

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="timeSelector">A optional <see cref="Func{{T, TResult}}"/> a function returning the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> polling interval. If specified, items are expired on a polling interval rather than per-item timers. Less accurate but more efficient when many items share similar expiration times.</param>
    /// <remarks>
    /// This overload uses periodic polling instead of per-item timers. Expired items are removed on the next
    /// poll after their timeout elapses, which trades accuracy for reduced timer overhead.
    /// </remarks>
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

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?}, TimeSpan?)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="timeSelector">A optional <see cref="Func{{T, TResult}}"/> a function returning the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">A optional <see cref="TimeSpan"/> if specified, items are expired on a polling interval rather than per-item timers.</param>
    /// <param name="scheduler"><see cref="IScheduler"/> the scheduler used to schedule polling and expiration timers.</param>
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
    /// Automatically removes items from the <see cref="ISourceCache{TObject, TKey}"/> after the timeout returned
    /// by <paramref name="timeSelector"/>. Returns an observable of the removed key-value pairs (not a changeset stream).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache from which expired items are removed.</param>
    /// <param name="timeSelector">A optional <see cref="Func{{T, TResult}}"/> a function returning the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">A optional <see cref="TimeSpan"/> if specified, items are expired on a polling interval rather than per-item timers.</param>
    /// <param name="scheduler">The scheduler used to schedule expiration timers. Defaults to <see cref="GlobalConfig.DefaultScheduler"/> if <see langword="null"/>.</param>
    /// <returns>An observable that emits the key-value pairs of items removed from the cache by expiration.</returns>
    /// <remarks>
    /// Unlike the stream-based overloads, this operates directly on the <see cref="ISourceCache{TObject, TKey}"/>
    /// and returns the removed items as <see cref="KeyValuePair{TKey, TObject}"/> collections,
    /// not as a changeset stream.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="timeSelector"/> is <see langword="null"/>.</exception>
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
    /// Filters items from the source changeset stream using a static predicate.
    /// Only items that satisfy <paramref name="filter"/> are included downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="filter"><see cref="Func{{T, TResult}}"/> the predicate used to determine whether each item is included.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance. Set to <see langword="false"/> to emit empty changesets, which can be useful for monitoring loading status.</param>
    /// <returns>An observable changeset containing only items that satisfy <paramref name="filter"/>.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If it passes, an <b>Add</b> is emitted. Otherwise the item is dropped.</description></item>
    /// <item><term>Update</term><description>Four outcomes: if both old and new values pass, an <b>Update</b> is emitted. If only the new value passes, an <b>Add</b> is emitted. If only the old value passed, a <b>Remove</b> is emitted. If neither passes, the change is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>The predicate is re-evaluated. If the item now passes but previously did not, an <b>Add</b> is emitted. If it still passes, a <b>Refresh</b> is forwarded. If it no longer passes, a <b>Remove</b> is emitted. If it still fails, the change is dropped.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <b>Refresh</b> events trigger re-evaluation, which can promote or demote items. Pair with <see cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/> for property-change-driven filtering.</para>
    /// </remarks>
    /// <seealso cref="FilterImmutable{TObject, TKey}"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, bool> filter,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.Filter.Static<TObject, TKey>.Create(
            source: source,
            filter: filter,
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <inheritdoc cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{Func{TObject, bool}}, IObservable{Unit}, bool)"/>
    /// <remarks>
    /// This overload does not accept a <c>reapplyFilter</c> signal. It is equivalent to calling the
    /// full dynamic overload with <see cref="Observable.Empty{TResult}()"/> as the reapply observable.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<Func<TObject, bool>> predicateChanged,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => source.Filter(
            predicateChanged: predicateChanged,
            reapplyFilter: Observable.Empty<Unit>(),
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Creates a dynamically filtered stream where the filter predicate depends on external state.
    /// Each emission from <paramref name="predicateState"/> triggers a full re-filtering of all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TState">The type of state value required by <paramref name="predicate"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="predicateState"><see cref="IObservable{T}"/> a stream of state values to be passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate"><see cref="Func{T, TResult}"/> a predicate that receives the current state and an item, returning <see langword="true"/> to include or <see langword="false"/> to exclude.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance. Set to <see langword="false"/> to emit empty changesets.</param>
    /// <returns>An observable changeset containing only items satisfying <paramref name="predicate"/> for the latest state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="predicateState"/>, or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="predicateState"/> should emit an initial value immediately upon subscription.
    /// Until the first state value arrives, no items pass the filter (all items are excluded).
    /// Each subsequent state emission triggers a full re-evaluation of every item in the collection.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Evaluated against the current state. If it passes, an <b>Add</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Update</term><description>Re-evaluated. Four outcomes as with the static <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> overload.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluated against the current state. May produce <b>Add</b>, <b>Refresh</b>, <b>Remove</b>, or be dropped.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <paramref name="predicateState"/> should emit an initial value immediately. Each emission triggers a full re-evaluation of all items, which can be expensive for large collections.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey, TState>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<TState> predicateState,
                Func<TState, TObject, bool> predicate,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.Filter.Dynamic<TObject, TKey, TState>.Create(
            source: source,
            predicateState: predicateState,
            predicate: predicate,
            reapplyFilter: Observable.Empty<Unit>(),
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <inheritdoc cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="predicateChanged"><see cref="IObservable{{T}}"/> an observable that emits new predicates. Each emission replaces the current predicate and triggers a full re-evaluation of all items.</param>
    /// <param name="reapplyFilter"><see cref="IObservable{{T}}"/> an observable that, when it emits, triggers a full re-evaluation of all items against the current predicate. Useful when filtering on mutable item properties.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance.</param>
    /// <remarks>
    /// In addition to the per-item behavior described in the static <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> overload,
    /// emissions from <paramref name="predicateChanged"/> replace the predicate and trigger full re-filtering,
    /// while emissions from <paramref name="reapplyFilter"/> re-evaluate all items against the current predicate.
    /// <para><b>Worth noting:</b> No items are included until the predicate observable emits its first value.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<Func<TObject, bool>> predicateChanged,
                IObservable<Unit> reapplyFilter,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull

        => Cache.Internal.Filter.Dynamic<TObject, TKey, Func<TObject, bool>>.Create(
            source: source,
            predicateState: predicateChanged,
            predicate: static (predicate, item) => predicate.Invoke(item),
            reapplyFilter: reapplyFilter,
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Creates a filtered stream, optimized for stateless/deterministic filtering of immutable items.
    /// </summary>
    /// <typeparam name="TObject">The type of collection items to be filtered.</typeparam>
    /// <typeparam name="TKey">The type of the key values of each collection item.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source stream of collection items to be filtered.</param>
    /// <param name="predicate"><see cref="Func{T, TResult}"/> the filtering predicate to be applied to each item.</param>
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
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If it passes, an <b>Add</b> is emitted. Otherwise the item is dropped.</description></item>
    /// <item><term>Update</term><description>Four outcomes: if both old and new values pass, an <b>Update</b> is emitted. If only the new value passes, an <b>Add</b> is emitted. If only the old value passed, a <b>Remove</b> is emitted. If neither passes, the change is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description><b>Dropped.</b> Because items are assumed immutable, there is nothing to re-evaluate.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
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
    /// Filters items using a per-item <see cref="IObservable{Boolean}"/> that controls inclusion.
    /// Each item's observable is created by <paramref name="filterFactory"/> and toggles the item in or out of the downstream stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="filterFactory">A factory that creates an <see cref="IObservable{Boolean}"/> for each item and its key. When the observable emits <see langword="true"/>, the item is included; when <see langword="false"/>, it is excluded.</param>
    /// <param name="buffer">A <see cref="TimeSpan"/> that optional time window to buffer inclusion changes from per-item observables before re-evaluating.</param>
    /// <param name="scheduler">An <see cref="IScheduler"/> that optional scheduler used for buffering.</param>
    /// <returns>An observable changeset containing only items whose per-item observable most recently emitted <see langword="true"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the per-item observable. The item is <b>not included downstream until the observable emits its first <see langword="true"/></b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's observable subscription and subscribes to the new item's observable. Inclusion state is reset; the new observable must emit before the item reappears.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's observable subscription. If the item was included downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently included downstream. Otherwise dropped.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (filter observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First <see langword="true"/></term><description>The item is included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="false"/> (was included)</term><description>The item is excluded: a <b>Remove</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was excluded)</term><description>The item is re-included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was included)</term><description>No effect (already included).</description></item>
    /// <item><term><see langword="false"/> (was excluded)</term><description>No effect (already excluded).</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains in its current inclusion state. No further toggling is possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible downstream until their per-item observable emits at least one <see langword="true"/>.
    /// If an item's observable never emits, the item never appears. The <paramref name="buffer"/> parameter batches
    /// rapid inclusion changes from per-item observables into a single re-evaluation, reducing changeset chatter.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="filterFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return new FilterOnObservable<TObject, TKey>(source, filterFactory, buffer, scheduler).Run();
    }

    /// <inheritdoc cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <remarks>
    /// This overload does not provide the key to <paramref name="filterFactory"/>; only the item is passed.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return source.FilterOnObservable((obj, _) => filterFactory(obj), buffer, scheduler);
    }

    /// <summary>
    /// Obsolete: do not use. This can cause unhandled exception issues. Use the standard Rx <c>Finally</c> operator instead.
    /// </summary>
    /// <typeparam name="T">The type contained within the observables.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="finallyAction"><see cref="Action{{T}}"/> the finally action.</param>
    /// <returns>An observable which has always a finally action applied.</returns>
    [Obsolete("This can cause unhandled exception issues so do not use")]
    public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        finallyAction.ThrowArgumentNullExceptionIfNull(nameof(finallyAction));

        return new FinallySafe<T>(source, finallyAction).Run();
    }

    /// <summary>
    /// Unwraps each <see cref="IChangeSet{TObject, TKey}"/> into individual <see cref="Change{TObject, TKey}"/>
    /// values via <see cref="Observable.SelectMany{TSource, TResult}(IObservable{TSource}, Func{TSource, IEnumerable{TResult}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable of individual <see cref="Change{TObject, TKey}"/> values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ForEachChange{TObject, TKey}"/>
    public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.SelectMany(changes => changes);
    }

    /// <summary>
    /// Merges a list of changesets (typically from an Rx <c>Buffer</c> operation) into a single changeset
    /// by concatenating all changes. Empty buffers are filtered out.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source observable of buffered changeset lists.</param>
    /// <returns>An observable changeset combining all changes from each buffer into a single emission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> FlattenBufferResult<TObject, TKey>(this IObservable<IList<IChangeSet<TObject, TKey>>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(x => x.Count != 0).Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every individual <see cref="Change{TObject,TKey}"/> in each changeset,
    /// regardless of change reason. The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="action">The action to invoke for each change. Receives the full <see cref="Change{TObject,TKey}"/> struct, including <see cref="Change{TObject,TKey}.Reason"/>, <see cref="Change{TObject,TKey}.Key"/>, <see cref="Change{TObject,TKey}.Current"/>, and <see cref="Change{TObject,TKey}.Previous"/>.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// All change reasons (Add, Update, Remove, Refresh) trigger the callback.
    /// Use <see cref="OnItemAdded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>,
    /// <see cref="OnItemUpdated{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TObject,TKey})"/>,
    /// <see cref="OnItemRemoved{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey}, bool)"/>, or
    /// <see cref="OnItemRefreshed{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>
    /// to target a specific reason.
    /// </para>
    /// <para>
    /// Implemented via Rx's <c>Do</c> operator on the changeset stream.
    /// Exceptions thrown in <paramref name="action"/> propagate as <c>OnError</c> to the subscriber. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<Change<TObject, TKey>> action)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.ForEach(action));
    }

    /// <inheritdoc cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left and right values into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>.</remarks>
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
    /// Joins two changeset streams, producing a result for every key that appears on either side (or both).
    /// Both sides are <see cref="Optional{T}"/> because a given key may only exist on one side at any point.
    /// Equivalent to SQL FULL OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left, and optional right into a destination object. Example: <c>(key, left, right) =&gt; new Result(key, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the left value and the matching right (or <c>Optional.None</c> if no right exists).</description></item>
    ///   <item><term>Update</term><description>Re-invokes <paramref name="resultSelector"/> with the new left value and current right (if any).</description></item>
    ///   <item><term>Remove</term><description>If a right match still exists, re-invokes the selector with left as <c>Optional.None</c>. If neither side remains, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the matching left (or <c>Optional.None</c>) and the right value.</description></item>
    ///   <item><term>Update</term><description>Re-invokes selector with current left (if any) and the new right value.</description></item>
    ///   <item><term>Remove</term><description>If a left match still exists, re-invokes the selector with right as <c>Optional.None</c>. If neither side remains, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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

    /// <inheritdoc cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
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
    /// Groups right-side items by their mapped key, then full-joins each group to the left source.
    /// A result is produced for every key that appears on either side (or both). The left value is
    /// <see cref="Optional{T}"/> because only the right side may have entries for a given key.
    /// Equivalent to SQL FULL OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left value, and the right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the left value and the current right group for that key (may be empty).</description></item>
    ///   <item><term>Update</term><description>Re-invokes <paramref name="resultSelector"/> with the new left value and current right group.</description></item>
    ///   <item><term>Remove</term><description>If the right group is non-empty, re-invokes with left as <c>Optional.None</c>. If both sides are empty, removes the result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group, then re-invokes selector with the current left (if any) and the updated group.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes selector.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty and no left exists, removes the result. Otherwise re-invokes selector.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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
    /// Groups items from the source changeset, producing groups only for group keys present in <paramref name="resultGroupSource"/>.
    /// Useful for parent-child relationships where parents and children come from different streams.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="groupSelector"><see cref="Func{{T, TResult}}"/> the group selector factory.</param>
    /// <param name="resultGroupSource">An <see cref="IObservable{T}"/> of <see cref="IDistinctChangeSet{TGroupKey}"/> used to determine which groups appear in the result.</param>
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
    /// Groups items from the source changeset by a key extracted via <paramref name="groupSelectorKey"/>.
    /// Each group is an observable sub-cache that receives changes for its members.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <returns>An observable that emits group changesets. Each group exposes a sub-cache of its members.</returns>
    /// <remarks>
    /// <para>
    /// Items are assigned to groups based on the value returned by <paramref name="groupSelectorKey"/>.
    /// Groups are created on demand when the first item is assigned, and removed when their last member is removed.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The group key is evaluated. The item is added to the corresponding group (creating the group if new). An <b>Add</b> is emitted to the group's sub-cache.</description></item>
    /// <item><term>Update</term><description>The group key is re-evaluated. If unchanged, an <b>Update</b> is emitted within the same group. If the key changed, the item is removed from the old group (emitting <b>Remove</b>) and added to the new group (emitting <b>Add</b>). An empty old group is removed.</description></item>
    /// <item><term>Remove</term><description>The item is removed from its group. If the group becomes empty, the group itself is removed from the output.</description></item>
    /// <item><term>Refresh</term><description>The group key is re-evaluated. If unchanged, a <b>Refresh</b> is forwarded within the group. If the key changed, the item moves between groups (Remove from old, Add to new).</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Each group is a live sub-cache that can be subscribed to independently. Subscribers
    /// to a group receive only changes for items in that group. When a group is removed (becomes empty),
    /// its sub-cache completes.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupWithImmutableState{TObject, TKey, TGroupKey}"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupOnProperty{TObject, TKey, TGroupKey}"/>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, null).Run();
    }

    /// <inheritdoc cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{{T, TResult}}"/> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <see cref="IObservable{{T}}"/> that when this observable emits, all items are re-evaluated against the group selector, potentially moving items between groups.</param>
    /// <returns>An observable that emits group changesets.</returns>
    /// <remarks>This overload adds a <paramref name="regrouper"/> signal. When it fires, every item in the cache is re-grouped using the current selector, which is useful when the grouping depends on mutable item state.</remarks>
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
    /// Groups items using a dynamically changing group selector function.
    /// Each time <paramref name="groupSelectorKeyObservable"/> emits a new selector, all items are re-grouped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="groupSelectorKeyObservable"><see cref="Func{T, TResult}"/> an observable that emits group selector functions. Each emission triggers a full re-grouping of all items.</param>
    /// <param name="regrouper">An <see cref="IObservable{T}"/> that optional signal to force re-evaluation of all items against the current selector.</param>
    /// <returns>An observable that emits group changesets.</returns>
    /// <remarks>
    /// <para>
    /// Unlike the static-selector overload, this accepts an observable of selector functions. When a new selector
    /// arrives, every item is re-evaluated and may move between groups. The optional <paramref name="regrouper"/>
    /// signal triggers re-evaluation without changing the selector (useful when item properties that affect grouping change).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The current selector determines the group. Item is added to the group (group created if new).</description></item>
    /// <item><term>Update</term><description>Group key re-evaluated. Item may move between groups if the key changed.</description></item>
    /// <item><term>Remove</term><description>Item removed from its group. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>Group key re-evaluated. Item may move between groups.</description></item>
    /// <item><term>OnError</term><description>Forwarded from source or from <paramref name="groupSelectorKeyObservable"/>.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));

        return new GroupOnDynamic<TObject, TKey, TGroupKey>(source, groupSelectorKeyObservable, regrouper).Run();
    }

    /// <inheritdoc cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{Func{TObject, TKey, TGroupKey}}, IObservable{Unit}?)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="groupSelectorKeyObservable"><see cref="IObservable{{T}}"/> an observable of selector functions that take only the item (not the key).</param>
    /// <param name="regrouper">An optional <see cref="IObservable{{T}}"/> optional signal to force re-evaluation.</param>
    /// <remarks>This overload accepts a selector that does not receive the key. Delegates to the overload accepting <c>Func&lt;TObject, TKey, TGroupKey&gt;</c>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));

        return source.Group(groupSelectorKeyObservable.Select(AdaptSelector<TObject, TKey, TGroupKey>), regrouper);
    }

    /// <summary>
    /// Groups items where each item's group key is determined by a per-item observable.
    /// The observable is created by <paramref name="groupObservableSelector"/> for each item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="groupObservableSelector">A <see cref="Func{T, TResult}"/> that factory that creates a group key observable for each item and its key.</param>
    /// <returns>An observable that emits group changesets. Each group is a live sub-cache of its members.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/> which evaluates
    /// the group key synchronously, this operator defers group assignment until the per-item observable emits.
    /// </para>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the per-item group key observable. The item is <b>not placed in any group until the observable emits its first group key</b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's group key subscription and subscribes to the new item's observable. The item is removed from its current group until the new observable emits.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's group key subscription. The item is removed from its current group. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The item remains in its current group.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (group key observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First value</term><description>The item is placed into the group matching the emitted key. An <b>Add</b> appears in that group's sub-cache. If the group is new, the group itself is added to the output.</description></item>
    /// <item><term>New value (different key)</term><description>The item moves: <b>Remove</b> from the old group, <b>Add</b> to the new group. If the old group becomes empty, it is removed from the output.</description></item>
    /// <item><term>Same value (unchanged key)</term><description>No effect (filtered by <c>DistinctUntilChanged</c>).</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains in its current group. No further group key changes are possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible (not in any group) until their per-item observable emits at least one
    /// group key. If an item's observable never emits, the item never appears in any group. Per-item observable errors
    /// terminate the entire stream. The output completes when the source completes and all per-item observables have
    /// also completed.
    /// </para>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnProperty{TObject, TKey, TGroupKey}"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="TransformOnObservable{TSource, TKey, TDestination}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, IObservable{TDestination}})"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="groupObservableSelector"><see cref="Func{{T, TResult}}"/> the group selector key.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="propertySelector"><see cref="Expression{{TDelegate}}"/> the property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">A optional <see cref="TimeSpan"/> a time span that indicates the throttle to wait for property change events.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="propertySelector"><see cref="Expression{{TDelegate}}"/> the property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">A optional <see cref="TimeSpan"/> a time span that indicates the throttle to wait for property change events.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> the scheduler.</param>
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
    /// Groups items by <paramref name="groupSelectorKey"/>, emitting immutable group snapshots instead of mutable sub-caches.
    /// Each group change contains a frozen copy of the group's state at that point in time.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <see cref="IObservable{T}"/> that optional signal to force re-evaluation of all items against the group selector.</param>
    /// <returns>An observable that emits immutable group changesets.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <see cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// in terms of how items are assigned to groups, but each group emission is an immutable snapshot.
    /// This makes it safe for parallel processing and eliminates race conditions on group state.
    /// The tradeoff is higher memory usage, since each change produces a new snapshot of the affected group.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item added to its group. An immutable snapshot of the group is emitted.</description></item>
    /// <item><term>Update</term><description>If group key unchanged, group snapshot re-emitted. If changed, item moves between groups; both affected groups emit new snapshots.</description></item>
    /// <item><term>Remove</term><description>Item removed from group. Updated snapshot emitted. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>Group key re-evaluated. If changed, item moves; affected group snapshots emitted.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TKey, TGroupKey}"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable which emits change sets.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="ignoreFunction"><see cref="Func{{T, TResult}}"/> the ignore function (current,previous)=>{ return true to ignore }.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="includeFunction"><see cref="Func{{T, TResult}}"/> the include function (current,previous)=>{ return true to include }.</param>
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

    /// <inheritdoc cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{{T, TResult}}"/> that combines the left and right values into a destination object. The composite key is not provided in this overload.</param>
    /// <remarks>Overload that omits the composite key from the result selector. Delegates to <see cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>.</remarks>
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
    /// Joins two changeset streams, producing a result only for keys that exist on both sides simultaneously.
    /// When either side loses its value for a key, the joined result is removed. Equivalent to SQL INNER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the composite key, left value, and right value into a destination object. Example: <c>((leftKey, rightKey), left, right) =&gt; new Result(leftKey, rightKey, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by a composite <c>(TLeftKey, TRightKey)</c> tuple.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching right value exists, invokes <paramref name="resultSelector"/> and emits an Add. If no right match, no emission.</description></item>
    ///   <item><term>Update</term><description>If a matching right exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes all joined results involving the removed left key.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching left value exists, invokes the selector and emits an Add.</description></item>
    ///   <item><term>Update</term><description>If a matching left exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result for this right key (if it was downstream).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>The output is keyed by a <c>(TLeftKey, TRightKey)</c> composite tuple, since a single left item may match multiple right items.</para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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

    /// <inheritdoc cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
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
    /// Groups right-side items by their mapped key, then inner-joins each group to the left source.
    /// A result is produced only when a left item and at least one right item share the same key.
    /// Equivalent to SQL INNER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a non-empty right group exists for this key, invokes <paramref name="resultSelector"/> and emits an Add. Otherwise no emission.</description></item>
    ///   <item><term>Update</term><description>If a right group exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result (if it was downstream).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If a matching left exists and the group was previously empty, emits an Add. If already joined, emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes the selector if a matching left exists.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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
    /// Calls <c>Evaluate()</c> on items that implement <see cref="IEvaluateAware"/> when a <b>Refresh</b> change arrives.
    /// Other change reasons are forwarded without invoking Evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Calls <c>Evaluate()</c> on the item, then forwards the change.</description></item>
    ///   <item><term>OnError</term><description>Forwarded to subscribers.</description></item>
    ///   <item><term>OnCompleted</term><description>Forwarded to subscribers.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : IEvaluateAware
        where TKey : notnull => source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Refresh).ForEach(u => u.Current.Evaluate()));

    /// <inheritdoc cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the optional right into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>.</remarks>
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
    /// Joins two changeset streams, producing a result for every left-side key. The right side is
    /// <see cref="Optional{T}"/> because a matching right item may or may not exist. All left items
    /// appear in the output regardless. Equivalent to SQL LEFT OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, left value, and optional right into a destination object. Example: <c>(key, left, right) =&gt; new Result(key, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the left value and matching right (or <c>Optional.None</c>).</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with the new left value and current right (if any).</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching left exists, re-invokes the selector (right transitions from None to Some) and emits an Update.</description></item>
    ///   <item><term>Update</term><description>If a matching left exists, re-invokes the selector with the new right value.</description></item>
    ///   <item><term>Remove</term><description>If a matching left exists, re-invokes the selector (right transitions from Some to None) and emits an Update.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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

    /// <inheritdoc cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
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
    /// Groups right-side items by their mapped key, then left-joins each group to the left source.
    /// A result is produced for every left-side key. The right group may be empty if no right items match.
    /// Equivalent to SQL LEFT OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the left value and the current right group (which may be empty).</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with the new left value and current right group.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If a matching left exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes the selector if a matching left exists.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If a matching left exists, re-invokes the selector (group may now be empty).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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
    /// Applies a FIFO size limit to the changeset stream. When the number of items exceeds <paramref name="size"/>,
    /// the oldest items are evicted and emitted as <b>Remove</b> changes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="size">The maximum number of items allowed. Must be greater than zero.</param>
    /// <returns>An observable changeset stream with size-limited contents.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded. If the cache exceeds the size limit, the oldest items are emitted as <b>Remove</b> changes.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term>OnError</term><description>Forwarded to subscribers.</description></item>
    ///   <item><term>OnCompleted</term><description>Forwarded to subscribers.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="size"/> is zero or negative.</exception>
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
    /// Operates directly on a <see cref="ISourceCache{TObject, TKey}"/>, removing the oldest items when the cache
    /// exceeds <paramref name="sizeLimit"/>. Returns an observable of the evicted key-value pairs (not a changeset stream).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to apply the size limit to.</param>
    /// <param name="sizeLimit">The maximum number of items allowed. Must be greater than zero.</param>
    /// <param name="scheduler">Optional scheduler for observing changes. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits batches of evicted key-value pairs whenever the cache exceeds the size limit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sizeLimit"/> is zero or negative.</exception>
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
    /// Subscribes to a child observable for each item in the source cache changeset stream and merges all child
    /// emissions into a single <see cref="IObservable{T}"/>. When an item is added, <paramref name="observableSelector"/>
    /// creates its child subscription. When updated, the previous child subscription is disposed and a new one is created.
    /// When removed, its child subscription is disposed. Refresh changes have no effect on subscriptions.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that produces a child observable for each source item.</param>
    /// <returns>An observable that emits values from all active child observables, interleaved by arrival order.</returns>
    /// <remarks>
    /// <para>
    /// This operator does not produce changesets. It produces a flat stream of <typeparamref name="TDestination"/>
    /// values, similar to Rx <c>SelectMany</c> but lifecycle-aware: child subscriptions track items entering and
    /// leaving the source cache.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> to create a child observable and subscribes to it. Emissions from the child flow into the merged output.</description></item>
    /// <item><term>Update</term><description>Disposes the previous child subscription and creates a new one for the updated item.</description></item>
    /// <item><term>Remove</term><description>Disposes the child subscription for the removed item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The child observable continues unchanged.</description></item>
    /// <item><term>OnError</term><description>Errors from child observables are silently swallowed (the child is unsubscribed). Errors from the source changeset stream terminate the merged output.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes only when the source completes <b>and</b> all active child observables have also completed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The output is a plain <see cref="IObservable{TDestination}"/>, not a changeset stream. If you need merged changesets, use <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    /// <seealso cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IDisposable})"/>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory function that receives both the item and its key, and returns a child observable.</param>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Merges multiple changeset streams that arrive dynamically into a single unified changeset stream.
    /// Each inner stream emitted by the outer observable is subscribed and its changes forwarded downstream.
    /// When multiple sources provide the same key, the first source to add it retains priority unless a
    /// comparer-based overload is used.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <remarks>
    /// <para>
    /// Each inner changeset stream is independently tracked in its own cache. When multiple sources provide the same key,
    /// this overload uses first-in-wins semantics: the value from whichever source added the key first is
    /// the one published downstream. To control which value wins for duplicate keys, use an overload that
    /// accepts an <see cref="IComparer{T}"/>, which selects the lowest-ordered value across all sources.
    /// An <see cref="IEqualityComparer{T}"/> can be provided separately to suppress no-op updates when
    /// the new value equals the currently published value for a key.
    /// </para>
    /// <para>
    /// <b>Overload families:</b> MergeChangeSets has 16 overloads organized along three axes:
    /// (1) <b>Source type</b>: dynamic (<c>IObservable&lt;IObservable&lt;IChangeSet&gt;&gt;</c>, sources arrive at runtime),
    /// pair (<c>source + other</c>, exactly two streams), or static (<see cref="IEnumerable{T}"/>, all sources known up front).
    /// (2) <b>Conflict resolution</b>: none (first-in-wins), <see cref="IComparer{T}"/> (lowest-ordered wins),
    /// <see cref="IEqualityComparer{T}"/> (suppresses duplicate updates), or both.
    /// (3) <b>Completion</b>: static overloads accept a <c>completable</c> flag; when <see langword="false"/>, the output never completes
    /// even after all sources finish (useful for "live" merge scenarios).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If no source has previously provided this key, an <b>Add</b> is emitted downstream. If another source already holds this key, the new value is tracked internally but not emitted (first-in-wins). With a comparer, the lowest-ordered value across all sources is selected and published instead.</description></item>
    /// <item><term>Update</term><description>If the updating source currently owns the downstream value for this key, an <b>Update</b> is emitted. If a comparer is provided and the update causes a different source's value to become the best candidate, an <b>Update</b> is emitted with that other source's value.</description></item>
    /// <item><term>Remove</term><description>If the removed value was the one published downstream, the operator scans all remaining sources for the same key. If another source still holds that key, an <b>Update</b> is emitted with the replacement value (selected by comparer if provided, otherwise the next available). If no other source holds the key, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the refreshed item matches the currently published value, the <b>Refresh</b> is forwarded. With a comparer, all sources are re-evaluated first; if a different value now wins, an <b>Update</b> is emitted instead of the Refresh.</description></item>
    /// <item><term>OnError</term><description>An error from any source (outer or inner) terminates the entire merged output.</description></item>
    /// <item><term>OnCompleted</term><description>For dynamic overloads, the output completes when the outer observable completes and all subscribed inner observables have also completed. For static overloads, completion depends on the <c>completable</c> parameter (default <see langword="true"/>).</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> When a source removes a key that was published downstream, the fallback to another
    /// source's value is emitted as an <b>Update</b> (not an Add). This can be surprising if you expect
    /// a Remove followed by an Add. Also, errors from any single inner source terminate the entire merged
    /// stream, so consider error handling within individual sources if isolation is needed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using a comparer to resolve key conflicts.
    /// When multiple sources provide the same key, the item ordering lowest according to <paramref name="comparer"/>
    /// is published downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using an equality comparer to suppress
    /// redundant updates. When an incoming value for a key is equal (per <paramref name="equalityComparer"/>)
    /// to the currently published value, the update is suppressed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using both a comparer for key conflict resolution
    /// and an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// Convenience overload that merges exactly two cache changeset streams into a single output.
    /// Uses first-in-wins semantics for key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the first changeset stream.</param>
    /// <param name="other"><see cref="IObservable{{T}}"/> the second changeset stream to merge with <paramref name="source"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="other"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Convenience overload that merges exactly two cache changeset streams, using a comparer for key conflict resolution.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the first changeset stream.</param>
    /// <param name="other"><see cref="IObservable{{T}}"/> the second changeset stream to merge with <paramref name="source"/>.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when both sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// Convenience overload that merges exactly two cache changeset streams, using an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the first changeset stream.</param>
    /// <param name="other"><see cref="IObservable{{T}}"/> the second changeset stream to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, or <paramref name="equalityComparer"/> is null.</exception>
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
    /// Convenience overload that merges exactly two cache changeset streams, using both a comparer and an equality comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the first changeset stream.</param>
    /// <param name="other"><see cref="IObservable{{T}}"/> the second changeset stream to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when both sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// Merges <paramref name="source"/> with additional changeset streams into a single output.
    /// Uses first-in-wins semantics for key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the primary changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to merge with <paramref name="source"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Merges <paramref name="source"/> with additional changeset streams, using a comparer for key conflict resolution.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the primary changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to merge with <paramref name="source"/>.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// Merges <paramref name="source"/> with additional changeset streams, using an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the primary changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, or <paramref name="equalityComparer"/> is null.</exception>
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
    /// Merges <paramref name="source"/> with additional changeset streams, using both a comparer and an equality comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the primary changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// Merges a fixed collection of cache changeset streams into a single unified output. All source streams are
    /// subscribed when the output observable is subscribed to.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the collection of changeset streams to merge.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <remarks>
    /// <para>
    /// When multiple sources provide items with the same key, this overload uses first-in-wins semantics:
    /// the first source to provide a key retains priority. Removing that source's item allows the next
    /// available value for that key (if any) to surface. To control which value wins, use an overload
    /// that accepts an <see cref="IComparer{T}"/>.
    /// </para>
    /// <para>
    /// An error from any source terminates the entire merged output.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using a comparer for key conflict
    /// resolution. When multiple sources provide the same key, the item ordering lowest according to
    /// <paramref name="comparer"/> is published downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the collection of changeset streams to merge.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using an equality comparer to
    /// suppress redundant updates. When an incoming value for a key is equal (per <paramref name="equalityComparer"/>)
    /// to the currently published value, the update is suppressed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the collection of changeset streams to merge.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using both a comparer for key
    /// conflict resolution and an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the collection of changeset streams to merge.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observableSelector"/> or <paramref name="comparer"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="comparer"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that optional comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child changeset stream and merges all child
    /// changes into a single flattened output stream. Child subscriptions track the parent item lifecycle:
    /// created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source (parent) cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying parent items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a parent item and its key, and returns a child cache changeset stream. Called once per parent Add/Update.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress no-op child updates. When a child key's new value equals the current value per this comparer, the update is not emitted.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that optional comparer to resolve child key conflicts when multiple parents contribute children with the same destination key. The lowest-ordered child value wins. Without a comparer, the first parent to provide a key retains priority.</param>
    /// <returns>A merged changeset stream containing all child items from all active parent subscriptions.</returns>
    /// <remarks>
    /// <para>
    /// This is the changeset-aware counterpart to <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>.
    /// Where MergeMany produces a flat <c>IObservable&lt;T&gt;</c>, MergeManyChangeSets produces an <c>IObservable&lt;IChangeSet&gt;</c>
    /// that tracks the full lifecycle of child items, including key conflict resolution across parents.
    /// </para>
    /// <para>
    /// <b>Parent-side change handling (source changeset events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> with the new parent item to obtain a child changeset stream, then subscribes. As the child stream emits changesets, those child items are merged into the output. The downstream observer sees <b>Add</b> changes for each new child item.</description></item>
    /// <item><term>Update</term><description>Disposes the previous parent's child subscription (removing all of its contributed child items from the output as <b>Remove</b> changes), then creates a new child subscription for the updated parent. The new child's items appear as <b>Add</b> changes.</description></item>
    /// <item><term>Remove</term><description>Disposes the parent's child subscription. All child items contributed by that parent are emitted as <b>Remove</b> changes in the output. If another parent also provides a child with the same destination key, that parent's value is promoted as an <b>Update</b> (not an Add).</description></item>
    /// <item><term>Refresh</term><description>No effect on the child subscription. The parent's child stream continues unchanged.</description></item>
    /// </list>
    /// <para>
    /// <b>Child-side change handling (changes arriving from child changeset streams):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the destination key is new, an <b>Add</b> is emitted. If another parent already contributed a child with the same key, the conflict is resolved by <paramref name="comparer"/> (lowest wins) or first-in-wins if no comparer. The losing value is tracked internally but not emitted.</description></item>
    /// <item><term>Update</term><description>If this parent currently owns the destination key downstream, an <b>Update</b> is emitted. With a comparer, all parents are re-evaluated for that key; a different parent's value may win, producing an <b>Update</b> to that value instead.</description></item>
    /// <item><term>Remove</term><description>If this parent's value was the one published downstream for that destination key, the operator scans other parents for the same key. If found, an <b>Update</b> is emitted with the replacement. If not, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the child item is the one currently published downstream, the <b>Refresh</b> is forwarded. With a comparer, all parents are re-evaluated first; if a different value now wins, an <b>Update</b> is emitted instead.</description></item>
    /// </list>
    /// <para>
    /// <b>Error and completion:</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>An error from the source (parent) stream or from any child changeset stream terminates the entire output. Unlike <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>, child errors are NOT swallowed.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when the source (parent) stream completes <b>and</b> all active child changeset streams have also completed.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> When multiple parents contribute children with the same destination key, only one value is published
    /// downstream at a time. The <paramref name="comparer"/> controls which value wins; without it, the first parent to add the key
    /// retains priority. Removing a parent that owned a contested key causes the next-best value (per comparer or next available)
    /// to surface as an <b>Update</b>, not an Add. The <paramref name="equalityComparer"/> independently controls whether a child
    /// Update for an already-published key is suppressed when the new value equals the old.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IObservable{IChangeSet{TDestination, TDestinationKey}}}, Func{TDestination, TDestinationKey})"/>
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
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
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
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control. The selector receives only the item.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
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
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
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
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with full control over all conflict resolution parameters.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child
    /// changes into a single flattened output. When multiple source items produce children with the same destination key,
    /// <paramref name="sourceComparer"/> determines which source has priority (the source ordering lower wins).
    /// If sources compare equal, <paramref name="childComparer"/> (if provided) breaks the tie.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{T}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/> (default), a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="childComparer">An <see cref="IComparer{T}"/> that optional fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream containing items from all active child streams, with conflicts resolved by source priority.</returns>
    /// <remarks>
    /// <para>
    /// The <paramref name="sourceComparer"/> provides a layer of conflict resolution above the child values themselves.
    /// This is useful when source items represent priority tiers (e.g., user settings overriding defaults).
    /// </para>
    /// <para>
    /// Errors from child streams propagate to the output. An error from the source or any child terminates the merged output.
    /// The output completes when the source completes and all active child streams have also completed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="sourceComparer"/> is null.</exception>
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
    /// For each item in the source cache, subscribes to a child list changeset stream produced by
    /// <paramref name="observableSelector"/> and merges all child changes into a single flattened list changeset output.
    /// Child subscriptions follow the source item lifecycle: created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and its key, and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
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
    /// For each item in the source cache, subscribes to a child list changeset stream and merges all child changes
    /// into a single flattened list changeset output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that factory function that receives a source item and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        return source.MergeManyChangeSets((obj, _) => observableSelector(obj), equalityComparer);
    }

    /// <summary>
    /// Like <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>,
    /// but wraps each emitted value as an <see cref="ItemWithValue{TObject, TValue}"/>, pairing the source item
    /// with the value it produced. This lets you identify which source item is responsible for each emission.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory function that produces a child observable for each source item.</param>
    /// <returns>An observable of <see cref="ItemWithValue{TObject, TValue}"/> pairing each emission with its source item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="MergeManyItems{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source cache changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory function that receives both the item and its key, and returns a child observable.</param>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Monitors the source observable and emits <see cref="ConnectionStatus"/> values: <c>Pending</c> initially,
    /// <c>Loaded</c> when the first value arrives, <c>Errored</c> on error, and <c>Completed</c> on completion.
    /// This is not a changeset operator.
    /// </summary>
    /// <typeparam name="T">The type of the source observable.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable to monitor.</param>
    /// <returns>An observable that emits <see cref="ConnectionStatus"/> values reflecting the source's lifecycle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source) => new StatusMonitor<T>(source).Run();

    /// <summary>
    /// Filters out empty changesets from the stream. A thin wrapper around <c>Where(changes =&gt; changes.Count != 0)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable that emits only non-empty changesets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="StartWithEmpty{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(changes => changes.Count != 0);
    }

    /// <summary>
    /// Filters and casts items in the changeset to <typeparamref name="TDestination"/>. Items that are not of type
    /// <typeparamref name="TDestination"/> are excluded. Combines filter and transform in one step without an intermediate cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The destination type to filter and cast to.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable changeset.</param>
    /// <param name="suppressEmptyChangeSets">If <see langword="true"/>, changesets that become empty after filtering are suppressed.</param>
    /// <returns>An observable changeset of <typeparamref name="TDestination"/> items.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>If the item is <typeparamref name="TDestination"/>, cast and emit as <b>Add</b>. Otherwise dropped.</description></item>
    ///   <item><term><b>Update</b></term><description>Re-evaluated. If the new item is <typeparamref name="TDestination"/>, emit accordingly. If the old item was downstream but the new one is not, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Remove</b></term><description>If the item was downstream, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Refresh</b></term><description>If the item is downstream, forwarded as <b>Refresh</b>.</description></item>
    ///   <item><term>OnError</term><description>Forwarded to subscribers.</description></item>
    ///   <item><term>OnCompleted</term><description>Forwarded to subscribers.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="addAction"><see cref="Action{T}"/> the callback invoked for each added item. Receives the new item and its key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Invokes <paramref name="addAction"/> with the item and key.</description></item>
    ///   <item><term>Update</term><description>Ignored.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="addAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="addAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemUpdated{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TObject,TKey})"/>
    /// <seealso cref="OnItemRemoved{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey}, bool)"/>
    /// <seealso cref="ForEachChange{TObject,TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> addAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));

        return source.OnChangeAction(ChangeReason.Add, addAction);
    }

    /// <inheritdoc cref="OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="addAction"><see cref="Action{{T}}"/> the callback invoked for each added item. Receives only the item (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> addAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemAdded((obj, _) => addAction(obj));

    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="refreshAction"><see cref="Action{{T}}"/> the callback invoked for each refreshed item. Receives the item and its key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored.</description></item>
    ///   <item><term>Update</term><description>Ignored.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Invokes <paramref name="refreshAction"/> with the item and key.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="refreshAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="refreshAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AutoRefresh{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> refreshAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        refreshAction.ThrowArgumentNullExceptionIfNull(nameof(refreshAction));

        return source.OnChangeAction(ChangeReason.Refresh, refreshAction);
    }

    /// <inheritdoc cref="OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="refreshAction"><see cref="Action{{T}}"/> the callback invoked for each refreshed item. Receives only the item (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> refreshAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRefreshed((obj, _) => refreshAction(obj));

    /// <summary>
    /// Invokes <paramref name="removeAction"/> for each item with <see cref="ChangeReason.Remove"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="removeAction"><see cref="Action{T}"/> the callback invoked for each removed item. Receives the removed item and its key.</param>
    /// <param name="invokeOnUnsubscribe">
    /// When <see langword="true"/> (the default), the callback is also invoked for <b>every item still in the cache</b>
    /// when the subscription is disposed. When <see langword="false"/>, only inline Remove changes trigger the callback.
    /// </param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored (but tracked internally when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>).</description></item>
    ///   <item><term>Update</term><description>Ignored (cache updated internally when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>).</description></item>
    ///   <item><term>Remove</term><description>Invokes <paramref name="removeAction"/> with the item and key.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Unsubscribe behavior:</b> when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>, the operator
    /// maintains an internal cache mirroring the stream. On disposal, it iterates all remaining items and
    /// invokes <paramref name="removeAction"/> for each. This is useful for cleanup logic (e.g. event unsubscription)
    /// that must run for items that were never explicitly removed.
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="removeAction"/> propagate as <c>OnError</c> during inline removes.
    /// During unsubscribe disposal, exceptions are not caught.
    /// </para>
    /// <para><b>Worth noting:</b> The action also fires for ALL remaining items when the subscription is disposed (unless <c>invokeOnUnsubscribe</c> is <see langword="false"/>). The action runs under a lock; avoid calling into other caches from within it.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="removeAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DisposeMany{TObject,TKey}"/>
    /// <seealso cref="SubscribeMany{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IDisposable})"/>
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

    /// <inheritdoc cref="OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey}, bool)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="removeAction"><see cref="Action{{T}}"/> the callback invoked for each removed item. Receives only the item (no key).</param>
    /// <param name="invokeOnUnsubscribe">When <see langword="true"/> (the default), also invoked for all remaining items on disposal.</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey}, bool)"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRemoved((obj, _) => removeAction(obj), invokeOnUnsubscribe);

    /// <summary>
    /// Invokes <paramref name="updateAction"/> for each item with <see cref="ChangeReason.Update"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="updateAction"><see cref="Action{T}"/> the callback invoked for each updated item. Receives the current value, previous value, and key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored.</description></item>
    ///   <item><term>Update</term><description>Invokes <paramref name="updateAction"/> with (current, previous, key). The previous value is always available for Update changes.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="updateAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="updateAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemAdded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>
    /// <seealso cref="ForEachChange{TObject,TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject, TKey> updateAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.OnChangeAction(static change => change.Reason == ChangeReason.Update, change => updateAction(change.Current, change.Previous.Value, change.Key));
    }

    /// <inheritdoc cref="OnItemUpdated{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="updateAction"><see cref="Action{{T}}"/> the callback invoked for each updated item. Receives only the current and previous values (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemUpdated{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemUpdated((cur, prev, _) => updateAction(cur, prev));

    /// <summary>
    /// Combines multiple changeset streams using logical OR (union). An item appears downstream if it exists in any source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the first source changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to combine with.</param>
    /// <returns>A changeset stream containing items present in any of the sources.</returns>
    /// <remarks>
    /// <para>
    /// Items are tracked via reference counting across all sources. An item appears downstream as long as
    /// at least one source contains it. When the last source holding a key removes it, the item is removed downstream.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If this is the first source to provide the key, an <b>Add</b> is emitted. If other sources already have the key, the reference count is incremented but no emission occurs.</description></item>
    /// <item><term>Update</term><description>If the item is currently downstream, an <b>Update</b> is emitted.</description></item>
    /// <item><term>Remove</term><description>Reference count decremented. If the count reaches zero (no source holds the key), a <b>Remove</b> is emitted. Otherwise no emission.</description></item>
    /// <item><term>Refresh</term><description>If the item is downstream, a <b>Refresh</b> is forwarded.</description></item>
    /// <item><term>OnError</term><description>An error from any source terminates the combined output.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when all sources have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <seealso cref="And{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Except{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Xor{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
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

    /// <inheritdoc cref="Or{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <param name="sources"><see cref="ICollection{T}"/> a fixed collection of changeset streams to combine.</param>
    /// <remarks>This overload accepts a pre-built collection of sources instead of a params array.</remarks>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted batch of items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to populate.</param>
    /// <param name="observable"><see cref="IObservable{{T}}"/> the observable that emits batches of items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <remarks>
    /// <para>Each emission from <paramref name="observable"/> is passed to <see cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject})"/>, producing one changeset per emission containing <b>Add</b> or <b>Update</b> events for each item. Errors from <paramref name="observable"/> propagate and terminate the subscription. Completion ends the subscription; the cache retains all items.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    /// <seealso cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <seealso cref="ToObservableChangeSet{TObject, TKey}(IObservable{IEnumerable{TObject}}, Func{TObject, TKey}, Func{TObject, TimeSpan?}, int, IScheduler?)"/>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return observable.Subscribe(source.AddOrUpdate);
    }

    /// <summary>
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache to populate.</param>
    /// <param name="observable"><see cref="IObservable{{T}}"/> the observable that emits individual items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return observable.Subscribe(source.AddOrUpdate);
    }

    /// <summary>
    /// Subscribes to the changeset stream and clones each changeset into the destination cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="destination"><see cref="ISourceCache{{TObject, TKey}}"/> the destination cache to populate.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from the source.</returns>
    /// <remarks>
    /// <para>
    /// Each changeset from the source is applied to the destination cache inside an Edit call.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The item is added to the destination cache via AddOrUpdate.</description></item>
    /// <item><term>Update</term><description>The item is updated in the destination cache via AddOrUpdate.</description></item>
    /// <item><term>Remove</term><description>The item is removed from the destination cache.</description></item>
    /// <item><term>Refresh</term><description>A Refresh is issued on the destination cache for the item.</description></item>
    /// <item><term>OnError</term><description>The subscription is terminated. The destination cache is not rolled back.</description></item>
    /// <item><term>OnCompleted</term><description>The subscription ends. The destination cache retains all items.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <seealso cref="PopulateFrom{TObject, TKey}(ISourceCache{TObject, TKey}, IObservable{IEnumerable{TObject}})"/>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <inheritdoc cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="IIntermediateCache{{TObject, TKey}}"/> the destination intermediate cache to populate.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <inheritdoc cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="destination"><see cref="LockFreeObservableCache{{TObject, TKey}}"/> the destination lock-free cache to populate.</param>
    /// <remarks>Overload that targets a <see cref="LockFreeObservableCache{TObject, TKey}"/>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, LockFreeObservableCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <summary>
    /// Projects the current cache state through <paramref name="resultSelector"/> after each modification.
    /// Emits a new value of <typeparamref name="TDestination"/> on every changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="resultSelector">Projects the current <see cref="IQuery{TObject, TKey}"/> snapshot to a result value.</param>
    /// <returns>An observable that emits a projected value after each changeset.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Cache updated, then <paramref name="resultSelector"/> invoked and result emitted.</description></item>
    /// <item><term>Update</term><description>Cache updated, then <paramref name="resultSelector"/> invoked and result emitted.</description></item>
    /// <item><term>Remove</term><description>Cache updated, then <paramref name="resultSelector"/> invoked and result emitted.</description></item>
    /// <item><term>Refresh</term><description>Cache updated, then <paramref name="resultSelector"/> invoked and result emitted.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The selector is called on every changeset, which can be chatty. The <see cref="IQuery{TObject, TKey}"/> exposes the full cache state for LINQ-style queries.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ToCollection{TObject, TKey}"/>
    /// <seealso cref="ToSortedCollection{TObject, TKey, TSortKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TSortKey}, SortDirection)"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="itemChangedTrigger">A <see cref="Func{{T, TResult}}"/> that should the query be triggered for observables on individual items.</param>
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
    /// Cache-aware equivalent of <c>Publish().RefCount()</c>. An internal cache is created on the first subscriber
    /// and disposed when the last subscriber unsubscribes. All subscribers share the same upstream subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>A ref-counted observable changeset stream.</returns>
    /// <seealso cref="AsObservableCache{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IObservable<IChangeSet<TObject, TKey>> RefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate the specified item. Produces a changeset with a single <b>Refresh</b> change.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to refresh.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a Refresh inside <see cref="ISourceCache{TObject,TKey}.Edit"/>. A Refresh does not change data in the cache; it signals downstream operators (such as <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> or <see cref="Sort{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, SortOptimisations, int)"/>) to re-evaluate the item.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Refresh</term><description>Produced for the specified item. Downstream operators re-evaluate this item against their current logic (filter predicate, sort comparer, group key selector, etc.).</description></item>
    /// <item><term>Other</term><description>No Add, Update, or Remove events are produced by this method.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="SuppressRefresh{TObject, TKey}"/>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(item));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate the specified items. Produces one changeset with a <b>Refresh</b> for each item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="items"><see cref="IEnumerable{{T}}"/> the items to refresh.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh(items));
    }

    /// <summary>
    /// Signals downstream operators to re-evaluate all items in the cache. Produces one changeset with a <b>Refresh</b> for every item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Refresh<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Refresh());
    }

    /// <summary>
    /// Removes the specified item from the cache. Produces a <b>Remove</b> changeset if the item exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to remove.</param>
    /// <remarks>
    /// <para>Convenience method that wraps a single-item removal inside <see cref="ISourceCache{TObject,TKey}.Edit"/>. The key is extracted from the item using the cache's key selector.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Remove</term><description>Produced if the key exists in the cache. The removed value is included in the changeset.</description></item>
    /// <item><term>Other</term><description>No Add, Update, or Refresh events are produced by this method.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, TObject)"/>
    /// <seealso cref="Clear{TObject, TKey}(ISourceCache{TObject, TKey})"/>
    /// <seealso cref="RemoveKeys{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TKey})"/>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(item));
    }

    /// <summary>
    /// Removes the item with the specified key from the cache. Produces a <b>Remove</b> changeset if the key exists, nothing otherwise.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <summary>
    /// Removes the specified items from the cache. Any items not present in the cache are ignored.
    /// Produces a <b>Remove</b> changeset for each item that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="items"><see cref="IEnumerable{{T}}"/> the items to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(items));
    }

    /// <summary>
    /// Removes the items with the specified keys from the cache. Any keys not present are ignored.
    /// Produces a <b>Remove</b> changeset for each key that existed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="keys"><see cref="IEnumerable{{T}}"/> the keys to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(keys));
    }

    /// <inheritdoc cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, TKey)"/>
    /// <param name="source"><see cref="IIntermediateCache{{TObject, TKey}}"/> the intermediate cache.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key of the item to remove.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(key));
    }

    /// <inheritdoc cref="Remove{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TKey})"/>
    /// <param name="source"><see cref="IIntermediateCache{{TObject, TKey}}"/> the intermediate cache.</param>
    /// <param name="keys"><see cref="IEnumerable{{T}}"/> the keys to remove.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.Remove(keys));
    }

    /// <summary>
    /// Strips the key from a cache changeset, converting <see cref="IChangeSet{TObject, TKey}"/> to
    /// <see cref="IChangeSet{TObject}"/> (list changeset). All indexed changes are dropped (sorting is not supported).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>A list changeset stream without key information.</returns>
    /// <seealso cref="ObservableListEx.AddKey{TObject, TKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TKey})"/>
    /// <seealso cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
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
    /// Removes a specific key from the cache. Equivalent to <c>source.Edit(u =&gt; u.RemoveKey(key))</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void RemoveKey<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.RemoveKey(key));
    }

    /// <summary>
    /// Removes multiple keys from the cache in a single <c>Edit</c> call. Keys not present in the cache are ignored.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="ISourceCache{{TObject, TKey}}"/> the source cache.</param>
    /// <param name="keys"><see cref="IEnumerable{{T}}"/> the keys to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(updater => updater.RemoveKeys(keys));
    }

    /// <inheritdoc cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left and right values into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>.</remarks>
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
    /// Joins two changeset streams, producing a result for every right-side key. The left side is
    /// <see cref="Optional{T}"/> because a matching left item may or may not exist. All right items
    /// appear in the output regardless. Equivalent to SQL RIGHT OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the right key, optional left, and right value into a destination object. Example: <c>(rightKey, left, right) =&gt; new Result(rightKey, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TRightKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the matching left (or <c>Optional.None</c>) and the right value.</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with current left (if any) and the new right value.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If matching right items exist, re-invokes the selector (left transitions from None to Some) and emits Updates.</description></item>
    ///   <item><term>Update</term><description>If matching right items exist, re-invokes the selector with the new left value.</description></item>
    ///   <item><term>Remove</term><description>If matching right items exist, re-invokes the selector (left transitions from Some to None) and emits Updates.</description></item>
    ///   <item><term>Refresh</term><description>If joined results exist, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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

    /// <inheritdoc cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left"><see cref="IObservable{{T}}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{{T}}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{{T, TResult}}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
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
    /// Groups right-side items by their mapped key, then right-joins each group to the left source.
    /// A result is produced for every key that has at least one right item. The left value is
    /// <see cref="Optional{T}"/> because a matching left item may or may not exist.
    /// Equivalent to SQL RIGHT OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TLeft, TLeftKey}"/> the left changeset stream.</param>
    /// <param name="right"><see cref="IObservable{T}"/> of <see cref="IChangeSet{TRight, TRightKey}"/> the right changeset stream.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If the group was previously empty, emits an Add with the current left (if any). Otherwise emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes <paramref name="resultSelector"/>.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a non-empty right group exists, re-invokes the selector (left transitions from None to Some) and emits an Update.</description></item>
    ///   <item><term>Update</term><description>If a non-empty right group exists, re-invokes the selector with the new left value.</description></item>
    ///   <item><term>Remove</term><description>If a non-empty right group exists, re-invokes the selector (left transitions from Some to None) and emits an Update.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
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
    /// Skips the initial snapshot changeset that <c>Connect()</c> typically emits, then forwards all subsequent changesets.
    /// Internally uses <c>DeferUntilLoaded().Skip(1)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable that skips the first changeset and forwards all others.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    /// <seealso cref="StartWithEmpty{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Obsolete: use SortAndBind instead. Sorts using the specified comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> the comparer.</param>
    /// <param name="sortOptimisations">A <see cref="SortOptimisations"/> that sort optimisation flags. Specify one or more sort optimisations.</param>
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
    /// Obsolete: use SortAndBind instead. Sorts using a dynamic comparer observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="comparerObservable"><see cref="IObservable{{T}}"/> the comparer observable.</param>
    /// <param name="sortOptimisations"><see cref="SortOptimisations"/> the sort optimisations.</param>
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
    /// Obsolete: use SortAndBind instead. Sorts using a dynamic comparer observable with a manual re-sort signal.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="comparerObservable"><see cref="IObservable{{T}}"/> the comparer observable.</param>
    /// <param name="resorter">An <see cref="IObservable{{T}}"/> that signal to instruct the algorithm to re-sort the entire data set.</param>
    /// <param name="sortOptimisations"><see cref="SortOptimisations"/> the sort optimisations.</param>
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
    /// Obsolete: use SortAndBind instead. Sorts using a static comparer with a manual re-sort signal.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> the comparer to sort on.</param>
    /// <param name="resorter">An <see cref="IObservable{{T}}"/> that signal to instruct the algorithm to re-sort the entire data set.</param>
    /// <param name="sortOptimisations"><see cref="SortOptimisations"/> the sort optimisations.</param>
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
    /// Sorts the changeset stream by the value returned from <paramref name="expression"/>. Creates a comparer internally
    /// and delegates to <see cref="Sort{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, SortOptimisations, int)"/>.
    /// Since Sort is obsolete, prefer SortAndBind for new code.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="expression">A <see cref="Func{{T, TResult}}"/> that expression that selects a comparable value from each item.</param>
    /// <param name="sortOrder"><see cref="SortDirection"/> the sort direction. Defaults to ascending.</param>
    /// <param name="sortOptimisations">A <see cref="SortOptimisations"/> that sort optimization flags.</param>
    /// <param name="resetThreshold">The number of updates before the entire list is re-sorted (rather than inline sort).</param>
    /// <returns>An observable that emits sorted changesets.</returns>
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
    /// Prepends an empty changeset to the source stream, ensuring subscribers always receive an immediate
    /// (empty) notification on subscription. Uses Rx's <c>StartWith</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable change set.</param>
    /// <returns>An observable that emits an empty changeset first, then all source changesets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(ChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable sorted change set.</param>
    /// <returns>An observable that emits an empty sorted changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="ISortedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(SortedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable virtual change set.</param>
    /// <returns>An observable that emits an empty virtual changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IVirtualChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable paged change set.</param>
    /// <returns>An observable that emits an empty paged changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IPagedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(PagedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable group change set.</param>
    /// <returns>An observable that emits an empty group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable immutable group change set.</param>
    /// <returns>An observable that emits an empty immutable group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IImmutableGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source read only collection observable.</param>
    /// <returns>An observable that emits an empty collection first, then all source collections.</returns>
    /// <remarks>Overload for <see cref="IReadOnlyCollection{T}"/>.</remarks>
    public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source) => source.StartWith(ReadOnlyCollectionLight<T>.Empty);

    /// <inheritdoc cref="StartWithItem{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TObject, TKey)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="item">The item to prepend. The key is extracted from <see cref="IKey{TKey}.Key"/>.</param>
    /// <remarks>Overload for items that implement <see cref="IKey{TKey}"/>. Delegates to the explicit key overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item)
        where TObject : IKey<TKey>
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.StartWithItem(item, item.Key);
    }

    /// <summary>
    /// Prepends a changeset containing a single <b>Add</b> for the given item and key to the source stream.
    /// The Rx equivalent of <c>StartWith</c>, but wrapped as a DynamicData changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to prepend.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key for the item.</param>
    /// <returns>An observable that emits a single-item Add changeset first, then all source changesets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var change = new Change<TObject, TKey>(ChangeReason.Add, key, item);
        return source.StartWith(new ChangeSet<TObject, TKey> { change });
    }

    /// <summary>
    /// Creates an <see cref="IDisposable"/> subscription per item via <paramref name="subscriptionFactory"/>.
    /// Subscriptions are created on Add/Update and disposed on Update/Remove. All active subscriptions
    /// are disposed when the stream completes, errors, or the subscription is disposed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="subscriptionFactory">Factory that creates an <see cref="IDisposable"/> for each item. Called on Add and Update (for the new value).</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="subscriptionFactory"/>, stores the returned <see cref="IDisposable"/>.</description></item>
    ///   <item><term>Update</term><description>Disposes the previous subscription, then calls <paramref name="subscriptionFactory"/> for the new value.</description></item>
    ///   <item><term>Remove</term><description>Disposes the subscription for the removed item.</description></item>
    ///   <item><term>Refresh</term><description>Passed through. No subscription change.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Internally implemented using <see cref="Transform{TDestination,TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Func{TObject,TKey,TDestination}, bool)"/>
    /// and <see cref="DisposeMany{TObject,TKey}"/>, so disposal semantics match <see cref="DisposeMany{TObject,TKey}"/>.
    /// </para>
    /// <para>
    /// Use this to tie per-item side effects (event subscriptions, polling timers, child observable subscriptions)
    /// to the lifecycle of items in the cache.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="subscriptionFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DisposeMany{TObject,TKey}"/>
    /// <seealso cref="OnItemRemoved{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey}, bool)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }

    /// <inheritdoc cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="subscriptionFactory">Factory that creates an <see cref="IDisposable"/> for each item. Receives the item and its key.</param>
    /// <remarks>Overload whose factory receives both the item and the key. See <see cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/> for full details.</remarks>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable change set.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SuppressRefresh<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.WhereReasonsAreNot(ChangeReason.Refresh);

    /// <inheritdoc cref="Switch{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <param name="sources">An observable that emits <see cref="IObservableCache{TObject, TKey}"/> instances.</param>
    /// <remarks>Overload that accepts observable caches. Internally calls <c>Connect()</c> on each cache and delegates to the changeset overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, unsubscribing from the previous one on each switch.
    /// When switching, the old source's items are removed and the new source's items are added.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources"><see cref="ICollection{T}"/> an observable that emits inner changeset streams.</param>
    /// <returns>A changeset stream reflecting the items from the most recently emitted inner source.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded from the active inner source.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded from the active inner source.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded from the active inner source.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Forwarded from the active inner source.</description></item>
    ///   <item><term>OnError</term><description>An error from any inner source or the outer source terminates the stream.</description></item>
    ///   <item><term>OnCompleted</term><description>Completes when the outer source and the current inner source have both completed.</description></item>
    /// </list>
    /// <para>On switch: <b>Remove</b> is emitted for all items from the previous source, then <b>Add</b> for all items from the new source.</para>
    /// <para><b>Worth noting:</b> Each switch clears the entire downstream cache before populating from the new source. Subscribers see a full remove-then-add reset on every switch.</para>
    /// </remarks>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items));

    /// <summary>
    /// Bridges a standard Rx observable of individual items into a DynamicData changeset stream.
    /// Each emission becomes an <b>Add</b> (or <b>Update</b> if the key already exists).
    /// Supports optional per-item expiration and size limiting.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable of individual items.</param>
    /// <param name="keySelector">A <see cref="Func{{T, TResult}}"/> that selects the unique key for each item.</param>
    /// <param name="expireAfter">A optional <see cref="Func{{T, TResult}}"/> optional: per-item expiration time. Return <see langword="null"/> for no expiration.</param>
    /// <param name="limitSizeTo">Optional: maximum cache size. Oldest items are removed when exceeded. Use -1 for no limit.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler for expiration timing.</param>
    /// <returns>An observable changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(
            this IObservable<TObject> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter = null,
            int limitSizeTo = -1,
            IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return Cache.Internal.ToObservableChangeSet<TObject, TKey>.Create(
            source: source,
            keySelector: keySelector,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);
    }

    /// <summary>
    /// Bridges a standard Rx observable of item batches into a DynamicData changeset stream.
    /// Each batch is processed with <c>AddOrUpdate</c>, producing <b>Add</b> or <b>Update</b> changes per item.
    /// Supports optional per-item expiration and size limiting.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source observable of item batches.</param>
    /// <param name="keySelector">A <see cref="Func{{T, TResult}}"/> that selects the unique key for each item.</param>
    /// <param name="expireAfter">A optional <see cref="Func{{T, TResult}}"/> optional: per-item expiration time. Return <see langword="null"/> for no expiration.</param>
    /// <param name="limitSizeTo">Optional: maximum cache size. Oldest items are removed when exceeded. Use -1 for no limit.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> optional scheduler for expiration timing.</param>
    /// <returns>An observable changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(
            this IObservable<IEnumerable<TObject>> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter = null,
            int limitSizeTo = -1,
            IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return Cache.Internal.ToObservableChangeSet<TObject, TKey>.Create(
            source: source,
            keySelector: keySelector,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);
    }

    /// <summary>
    /// Watches a single key in the source changeset stream, emitting <c>Optional.Some(value)</c> when the key
    /// is present and <c>Optional.None</c> when it is removed. Duplicate values are suppressed via <paramref name="equalityComparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to watch.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional comparer to suppress duplicate emissions. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable of <see cref="Optional{TObject}"/> that reflects the presence or absence of the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>, this emits <c>None</c> on removal
    /// (rather than the removed value), making it possible to distinguish "key is absent" from "key has a value".
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits <c>Optional.Some(value)</c> if the key was not previously tracked.</description></item>
    /// <item><term>Update</term><description>Emits <c>Optional.Some(newValue)</c> if the new value differs from the previous per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// <item><term>Remove</term><description>Emits <c>Optional.None</c>.</description></item>
    /// <item><term>Refresh</term><description>Emits <c>Optional.Some(value)</c> if the value differs from the last emission per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. To get an initial <c>None</c> when the key is absent, use the overload with <c>initialOptionalWhenMissing: true</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key value.</param>
    /// <param name="initialOptionalWhenMissing">Indicates if an initial Optional None should be emitted if the value doesn't exist.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Uses lock-based coordination. If the key exists synchronously on <c>Connect()</c>, the initial <c>None</c> may or may not be emitted depending on timing.</para>
    /// </remarks>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, bool initialOptionalWhenMissing, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (initialOptionalWhenMissing)
        {
            var seenValue = false;
            var locker = InternalEx.NewLock();

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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="sort"><see cref="Func{{T, TResult}}"/> the sort function.</param>
    /// <param name="sortOrder"><see cref="SortDirection"/> the sort order. Defaults to ascending.</param>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> the sort comparer.</param>
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

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update). The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, _) => transformFactory(current), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update). The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, key) => transformFactory(current, key), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new Transform<TDestination, TSource, TKey>(source, transformFactory, transformOnRefresh: transformOnRefresh).Run();
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts an optional <c>forceTransform</c> predicate filtering by source item only (without the key). The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, _) => transformFactory(current), forceTransform?.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts an optional <c>forceTransform</c> predicate filtering by source item and key. The factory receives the current item and key.</remarks>
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
    /// Projects each item in the changeset to a new form using a synchronous transform factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TSource, TKey}"/>.</param>
    /// <param name="transformFactory"><see cref="Func{T, TResult}"/> a function that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="forceTransform">An observable that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. Re-transformed items are emitted as <see cref="ChangeReason.Update"/> changes. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Transform maintains a 1:1 mapping between source and destination items, keyed identically. The factory
    /// is called once per Add and once per Update. Removes are forwarded without calling the factory.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls factory (receives current item, previous item, key), emits Update with Previous preserved.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh without re-transforming. To re-transform on Refresh, use the <paramref name="forceTransform"/> parameter or the <c>transformOnRefresh</c> overloads.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> By default, <b>Refresh</b> does NOT re-invoke the transform factory (it is just forwarded). Set <c>transformOnRefresh: true</c> to re-transform on <b>Refresh</b>.</para>
    /// <para>
    /// When <paramref name="forceTransform"/> emits a predicate, every cached item is tested against it.
    /// Matching items are re-transformed and emitted as Updates.
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>, terminating the stream.
    /// Use <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// to catch factory errors without killing the stream.
    /// </para>
    /// </remarks>
    /// <seealso cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <seealso cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <seealso cref="TransformImmutable{TDestination, TSource, TKey}"/>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
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

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits. The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.Transform((cur, _, _) => transformFactory(cur), forceTransform.ForForced<TSource, TKey>());

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits. The factory receives the current item and key.</remarks>
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

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits.</remarks>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a simpler factory that receives only the current item.</remarks>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives the current item and key.</remarks>
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
    /// Async version of <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// Projects each item using an async factory that returns <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TSource, TKey}"/>.</param>
    /// <param name="transformFactory"><see cref="Func{T, TResult}"/> an async function that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="forceTransform">An observable that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. Re-transformed items are emitted as <see cref="ChangeReason.Update"/> changes. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Transforms within a single changeset batch execute concurrently. The entire batch must complete
    /// before the resulting changeset is emitted. Use the <see cref="TransformAsyncOptions"/> overloads
    /// to control maximum concurrency and Refresh handling.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Awaits factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Awaits factory (receives current, previous, key), emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh by default. Use <see cref="TransformAsyncOptions.TransformOnRefresh"/> to re-transform.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Transforms are batched per changeset (all tasks must complete before the next changeset is processed). Completion waits for in-flight transforms. <b>Remove</b> does NOT cancel in-flight transforms for the removed key.</para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>. Use
    /// <see cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// to catch factory errors without terminating the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives only the current item.</remarks>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives the current item and key.</remarks>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
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
    /// Optimized transform for immutable items with deterministic (pure) transform functions.
    /// Refresh changes are dropped entirely since immutable items cannot change in place.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset observable.</param>
    /// <param name="transformFactory"><see cref="Func{{T, TResult}}"/> a pure function that maps a source item to a destination item. Must be deterministic: same input always produces equivalent output.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Because the transform is assumed to be stateless and deterministic, this operator does not track
    /// previously transformed items. This reduces memory overhead compared to <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls factory, emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>DROPPED. Immutable items do not change, so Refresh is meaningless.</description></item>
    /// </list>
    /// <para>Use this when items are immutable, the factory is pure, and the factory is cheap. If any of these conditions are false, use <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
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
    /// Flattens each source item into zero or more destination items (1:N), producing a single flat changeset.
    /// Each child item must have a globally unique key across all parents.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset of parent items.</param>
    /// <param name="manySelector">A function that expands a parent item into its children. For <see cref="ObservableCollection{T}"/> or <see cref="IObservableCache{TObject, TKey}"/> overloads, subsequent changes to the child collection are automatically tracked.</param>
    /// <param name="keySelector">A <see cref="Func{{T, TResult}}"/> that extracts a unique key from each child item. Keys must be unique across ALL parents, not just within one parent.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="manySelector"/>, emits Add for each child.</description></item>
    ///   <item><term>Update</term><description>Diffs old children vs new children: emits Remove for removed children, Add for new children, Update for children with matching keys.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove for all children of the removed parent.</description></item>
    ///   <item><term>Refresh</term><description>Propagated as Refresh to all children (no re-expansion).</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If two source items produce children with the same key, last-in-wins. <b>Refresh</b> does NOT re-expand children (only <b>Update</b> does).</para>
    /// <para>If two parents produce children with the same key, last-in-wins. Use the async variant with a <see cref="IComparer{T}"/> to control conflict resolution.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="ObservableCollection{T}"/> selector. Changes to the child collection (adds, removes, replacements) are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts a <see cref="ReadOnlyObservableCollection{T}"/> selector. Changes to the child collection are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="IObservableCache{TObject, TKey}"/> selector. The child cache is live: subsequent changes to it are automatically propagated downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>.
    /// Flattens each source item into zero or more destination items using an async factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset of parent items.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{{T, TResult}}"/> that extracts a unique key from each child item.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional comparer to determine if two child items with the same key are equal. Used to suppress no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents. The winning item is determined by this comparer.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para>
    /// Because each parent's expansion is async, child collections may arrive via separate changesets
    /// (unlike the synchronous <c>TransformMany</c> which batches all children into one changeset).
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>. Use
    /// <see cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// to catch errors without killing the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
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

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live: its changes propagate downstream. No <c>keySelector</c> is needed since the cache already has keys. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), equalityComparer, comparer);

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// with error handling. Factory exceptions are caught and routed to <paramref name="errorHandler"/> instead of
    /// terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset of parent items.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{{T, TResult}}"/> that extracts a unique key from each child item.</param>
    /// <param name="errorHandler">A <see cref="Action{{T}}"/> that called when <paramref name="manySelector"/> throws. The faulting item is skipped and the stream continues.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{T}"/> that optional comparer to determine if two child items with the same key are equal.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>Because the transformations are asynchronous, each sub-collection may be emitted via a separate changeset.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
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

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
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

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives the source item and its key.</remarks>
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

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Projects each item into a per-item observable. The latest value emitted by each item's observable
    /// becomes the transformed value in the output changeset.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TSource, TKey}"/>.</param>
    /// <param name="transformFactory">A function that, given a source item and its key, returns an <see cref="IObservable{T}"/> whose emissions become the transformed values.</param>
    /// <returns>An observable changeset where each key's value is the latest emission from its per-item observable.</returns>
    /// <remarks>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="transformFactory"/> and subscribes to the returned observable. The item is <b>not visible downstream until the observable emits its first value</b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's observable subscription and subscribes to the new item's observable. The item disappears from downstream until the new observable emits.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's observable subscription. If the item was visible downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently visible downstream. Otherwise dropped.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (transform observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First value</term><description>The transformed item appears downstream as an <b>Add</b>.</description></item>
    /// <item><term>Subsequent values</term><description>Each new value replaces the previous one: an <b>Update</b> is emitted downstream.</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains at its last emitted value. No further updates are possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible downstream until their per-item observable emits at least one value.
    /// If an item's observable never emits, that item never appears in the output. The transform factory's selector
    /// runs under an internal lock, so it must not synchronously access other DynamicData caches (deadlock risk in
    /// cross-cache pipelines). The output completes when the source completes and all per-item observables have
    /// also completed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, TDestination}, bool)"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}})"/>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformOnObservable<TSource, TKey, TDestination>(source, transformFactory).Run();
    }

    /// <inheritdoc cref="TransformOnObservable{TSource, TKey, TDestination}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, IObservable{TDestination}})"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformOnObservable((obj, _) => transformFactory(obj));
    }

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a simpler factory that receives only the current item, and a forceTransform predicate filtering by source item only.</remarks>
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

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a factory that receives the current item and key.</remarks>
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
    /// Projects each item using a synchronous factory, catching factory exceptions via a mandatory error handler
    /// instead of terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset observable.</param>
    /// <param name="transformFactory"><see cref="Func{T, TResult}"/> a function that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="errorHandler">Called when <paramref name="transformFactory"/> throws. Receives an <see cref="Error{TSource, TKey}"/> containing the exception and the faulting item. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <see cref="IObservable{{T}}"/> an observable that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// except that factory exceptions are routed to <paramref name="errorHandler"/> instead of propagating as <see cref="IObserver{T}.OnError"/>.
    /// Source-level errors (i.e. the source observable itself erroring) still propagate normally.
    /// </para>
    /// <para><b>Worth noting:</b> Factory exceptions are caught per-item; the faulting item is skipped and reported to the error handler while the stream continues. Source-level errors still terminate the stream.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
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

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.TransformSafe((cur, _, _) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives the current item and key.</remarks>
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

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items.</remarks>
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

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives only the current item.</remarks>
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

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives the current item and key.</remarks>
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
    /// Async version of <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// Projects each item using an async factory, catching factory exceptions via a mandatory error handler.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset observable.</param>
    /// <param name="transformFactory"><see cref="Func{T, TResult}"/> an async function that produces a <typeparamref name="TDestination"/>.</param>
    /// <param name="errorHandler">A <see cref="Action{{T}}"/> that called when <paramref name="transformFactory"/> throws or faults. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <see cref="IObservable{{T}}"/> optional observable to force re-transformation of matching items.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>Combines the async execution model of <see cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/> with the error-safe behavior of <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
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

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives only the current item.</remarks>
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

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives the current item and key.</remarks>
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

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
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
    /// Builds a hierarchical tree from a flat changeset using a parent key selector.
    /// Each item becomes a <see cref="Node{TObject, TKey}"/> with Parent, Children, Depth, and IsRoot properties.
    /// </summary>
    /// <typeparam name="TObject">The type of the source items. Must be a reference type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset of flat items.</param>
    /// <param name="pivotOn"><see cref="Func{{T, TResult}}"/> a function that returns the key of an item's parent. Return the item's own key (or a non-existent key) for root items.</param>
    /// <param name="predicateChanged">An <see cref="IObservable{T}"/> that optional observable that emits a filter predicate for nodes. When the predicate changes, nodes are re-evaluated and filtered.</param>
    /// <returns>An observable changeset of <see cref="Node{TObject, TKey}"/> items representing the tree.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Creates node, attaches to parent (or root if parent not found), emits Add.</description></item>
    ///   <item><term>Update</term><description>Updates node. If <paramref name="pivotOn"/> returns a different parent key, the node is re-parented.</description></item>
    ///   <item><term>Remove</term><description>Removes node. Orphaned children become root nodes.</description></item>
    ///   <item><term>Refresh</term><description>Re-evaluates parent key. May re-parent the node if the parent changed.</description></item>
    /// </list>
    /// <para>Circular references are NOT detected. If item A is the parent of B and B is the parent of A, behavior is undefined.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pivotOn"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn, IObservable<Func<Node<TObject, TKey>, bool>>? predicateChanged = null)
        where TObject : class
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pivotOn.ThrowArgumentNullExceptionIfNull(nameof(pivotOn));

        return new TreeBuilder<TObject, TKey>(source, pivotOn, predicateChanged).Run();
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> and does not provide an error handler (factory exceptions propagate as OnError).</remarks>
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

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload does not provide an error handler (factory exceptions propagate as OnError). The <c>transformOnRefresh</c> parameter controls Refresh behavior.</remarks>
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

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> but includes an error handler for factory/update action exceptions.</remarks>
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
    /// Projects each item using a transform factory for Add, and mutates the existing transformed
    /// item in place (via an update action) for Update, preserving the original object reference.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items. Must be a reference type since items are mutated in place.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TSource, TKey}"/>.</param>
    /// <param name="transformFactory">A <see cref="Func{T, TResult}"/> that called on Add (and optionally Refresh) to create a new <typeparamref name="TDestination"/>.</param>
    /// <param name="updateAction">A <see cref="Action{{T}}"/> that called on Update. Receives <c>(existingTransformed, newSource)</c>. Mutate the existing transformed item to reflect the new source value. Example: <c>(vm, model) =&gt; vm.Value = model.Value</c>.</param>
    /// <param name="errorHandler">A <see cref="Action{{T}}"/> that called when <paramref name="transformFactory"/> or <paramref name="updateAction"/> throws. The faulting item is skipped.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh changes call <paramref name="updateAction"/> on the existing item.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// This is useful when the destination type is a ViewModel that should maintain its identity across updates.
    /// Instead of replacing the entire ViewModel, the update action patches the existing instance.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="transformFactory"/>, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls <paramref name="updateAction"/> on the EXISTING transformed item (same reference), emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove.</description></item>
    ///   <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is true, calls <paramref name="updateAction"/>. Otherwise forwarded as Refresh.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, <paramref name="updateAction"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
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
    /// Emits <see langword="true"/> when all items in the cache satisfy a condition based on their per-item observable,
    /// and <see langword="false"/> otherwise. Re-evaluates whenever the cache changes or any per-item observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value emitted by each per-item observable.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{{T, TResult}}"/> that predicate applied to each per-item observable's latest value.</param>
    /// <returns>An observable of <c>bool</c> that emits whenever the all-items condition changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="equalityCondition"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>A new per-item subscription is created. The aggregate condition is recalculated.</description></item>
    /// <item><term>Update</term><description>The item is replaced in the collection snapshot. Condition recalculated.</description></item>
    /// <item><term>Remove</term><description>Per-item subscription disposed. Condition recalculated over remaining items.</description></item>
    /// <item><term>Refresh</term><description>No effect on per-item subscriptions. Condition not recalculated unless the per-item observable emits.</description></item>
    /// <item><term>OnError</term><description>An error from any per-item observable terminates the entire stream. Source errors also terminate.</description></item>
    /// <item><term>OnCompleted</term><description>Completes when the source and all per-item observables have completed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Items whose per-item observable has not yet emitted are treated as not satisfying the condition. An empty cache is vacuously <see langword="true"/>. The result uses <c>DistinctUntilChanged</c>, so duplicate <c>bool</c> values are suppressed.</para>
    /// </remarks>
    /// <seealso cref="TrueForAny{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TValue, bool})"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that selector which returns the target observable.</param>
    /// <param name="equalityCondition"><see cref="Func{{T, TResult}}"/> the equality condition.</param>
    /// <returns>An observable which boolean values indicating if true.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

    /// <summary>
    /// Emits <see langword="true"/> when any item in the cache satisfies a condition based on its per-item observable,
    /// and <see langword="false"/> when none do. Re-evaluates whenever the cache changes or any per-item observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value emitted by each per-item observable.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{{T, TResult}}"/> that predicate applied to each item and its per-item observable's latest value.</param>
    /// <returns>An observable of <c>bool</c> that emits whenever the any-item condition changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="equalityCondition"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>A new per-item subscription is created. The aggregate condition is recalculated.</description></item>
    /// <item><term>Update</term><description>The item is replaced in the collection snapshot. Condition recalculated.</description></item>
    /// <item><term>Remove</term><description>Per-item subscription disposed. Condition recalculated over remaining items.</description></item>
    /// <item><term>Refresh</term><description>No effect on per-item subscriptions. Condition not recalculated unless the per-item observable emits.</description></item>
    /// <item><term>OnError</term><description>An error from any per-item observable terminates the entire stream. Source errors also terminate.</description></item>
    /// <item><term>OnCompleted</term><description>Completes when the source and all per-item observables have completed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Items whose per-item observable has not yet emitted are treated as not satisfying the condition. An empty cache yields <see langword="false"/>. The result uses <c>DistinctUntilChanged</c>, so duplicate <c>bool</c> values are suppressed.</para>
    /// </remarks>
    /// <seealso cref="TrueForAll{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TValue, bool})"/>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

    /// <inheritdoc cref="TrueForAny{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TObject, TValue, bool})"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{{T, TResult}}"/> that factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{{T, TResult}}"/> that predicate applied to each per-item observable's latest value (without the item).</param>
    /// <remarks>This overload accepts a predicate that takes only the value, not the item. Useful when the condition depends only on the observed value.</remarks>
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
    /// Sets the <c>Index</c> property on each item (which must implement <see cref="IIndexAware"/>)
    /// to reflect its position in the sorted output. Operates on <see cref="ISortedChangeSet{TObject, TKey}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source sorted changeset stream.</param>
    /// <returns>An observable that emits the sorted changesets after updating item indices.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : IIndexAware
        where TKey : notnull => source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));

    /// <summary>
    /// Filters the source changeset stream to a single key, emitting each <see cref="Change{TObject, TKey}"/> for that key.
    /// Changes for all other keys are ignored.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of <see cref="Change{TObject, TKey}"/> for the specified key only.</returns>
    /// <remarks>
    /// <para>
    /// Emits Add, Update, Remove, and Refresh changes as they occur for the target key.
    /// No initial emission occurs if the key is not yet present in the cache. This operator does not
    /// produce changesets; it produces individual change notifications. For Optional-based watching,
    /// use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
    }

    /// <summary>
    /// Filters the source changeset stream to a single key, emitting the current value each time it changes.
    /// Even emits the value on removal (the removed item's value).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of the item's value whenever it changes for the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>,
    /// this does not emit <c>Optional.None</c> on removal. It emits the removed item's value instead.
    /// If you need to distinguish presence from absence, use ToObservableOptional.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits the added item's value.</description></item>
    /// <item><term>Update</term><description>Emits the new value.</description></item>
    /// <item><term>Remove</term><description>Emits the removed item's value (not <c>None</c>; use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/> if you need removal detection).</description></item>
    /// <item><term>Refresh</term><description>Emits the current value.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. Changes to other keys are ignored entirely.</para>
    /// </remarks>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }

    /// <inheritdoc cref="WatchValue{TObject, TKey}(IObservableCache{TObject, TKey}, TKey)"/>
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <remarks>This overload extends <see cref="IObservable{T}">IObservable</see>&lt;<see cref="IChangeSet{TObject, TKey}"/>&gt; instead of <see cref="IObservableCache{TObject, TKey}"/>.</remarks>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }

    /// <summary>
    /// Emits an item whenever any of its properties change via <see cref="INotifyPropertyChanged"/>.
    /// Subscribes to PropertyChanged on each cache item using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="propertiesToMonitor">Specific property names to monitor. If empty, all property changes trigger emissions.</param>
    /// <returns>An observable that emits the item itself each time a monitored property changes.</returns>
    /// <remarks>
    /// <para>
    /// Subscriptions are managed per item: created on Add, replaced on Update, disposed on Remove.
    /// Errors from individual property subscriptions are silently ignored. The output is not a changeset
    /// stream; it is a plain <c>IObservable&lt;TObject?&gt;</c>. If the same item changes multiple properties
    /// rapidly, each change emits the item separately (no deduplication).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to PropertyChanged on the new item.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's PropertyChanged subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Errors from individual property subscriptions are silently ignored. Source errors terminate the stream.</description></item>
    /// <item><term>OnCompleted</term><description>Completes when the source changeset stream completes.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="WhenValueChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }

    /// <summary>
    /// Emits a <see cref="PropertyValue{TObject, TValue}"/> (item + property value) whenever the specified property
    /// changes on any item in the cache. Subscribes via <see cref="INotifyPropertyChanged"/> using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{{TDelegate}}"/> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of <see cref="PropertyValue{TObject, TValue}"/> containing both the item and its property value.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. The output is not a changeset stream. If you only need
    /// the value (not the owning item), use <see cref="WhenValueChanged{TObject, TKey, TValue}"/> instead.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the specified property on the new item. If <c>notifyOnInitialValue</c> is true, the current value is emitted immediately.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's property subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's property subscription. No further emissions for this item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The existing property subscription continues.</description></item>
    /// <item><term>OnError</term><description>Per-item property subscription errors are silently ignored. Source errors terminate the stream.</description></item>
    /// <item><term>OnCompleted</term><description>Completes when the source changeset stream completes.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.MergeMany(t => t.WhenPropertyChanged(propertyAccessor, notifyOnInitialValue));
    }

    /// <summary>
    /// Emits the property value whenever the specified property changes on any item in the cache.
    /// Like <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> but emits only the value, discarding the owning item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{TDelegate}"/> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of property values. The owning item is not included; use <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> if you need it.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. If you need to correlate a value back to its source item,
    /// use <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> which returns a <see cref="PropertyValue{TObject, TValue}"/> pair.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the specified property. If <c>notifyOnInitialValue</c> is true, the current value is emitted immediately.</description></item>
    /// <item><term>Update</term><description>Disposes the old subscription, subscribes to the new item's property.</description></item>
    /// <item><term>Remove</term><description>Disposes the property subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Per-item errors silently ignored. Source errors terminate the stream.</description></item>
    /// <item><term>OnCompleted</term><description>Completes when the source completes.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject, TKey}"/>
    /// <seealso cref="AutoRefresh{TObject, TKey, TProperty}(IObservable{IChangeSet{TObject, TKey}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="reasons"><see cref="ChangeReason"/> the reasons.</param>
    /// <returns>An observable which emits a change set with items matching the reasons.</returns>
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes will cause memory leaks in downstream caches, since items are never cleaned up.</para>
    /// </remarks>
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
    /// <param name="source"><see cref="IObservable{{T}}"/> the source changeset stream.</param>
    /// <param name="reasons"><see cref="ChangeReason"/> the reasons.</param>
    /// <returns>An observable which emits a change set with items not matching the reasons.</returns>
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes will cause memory leaks in downstream caches, since items are never cleaned up.</para>
    /// </remarks>
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
    /// Combines multiple changeset streams using logical XOR (symmetric difference).
    /// An item appears downstream only if it exists in exactly one source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the first source changeset stream.</param>
    /// <param name="others">An <see cref="IEnumerable{T}"/> that additional changeset streams to combine with.</param>
    /// <returns>A changeset stream containing items present in exactly one source.</returns>
    /// <remarks>
    /// <para>
    /// Items are tracked via reference counting. An item appears downstream only when exactly one
    /// source holds it. Adding the same key from a second source removes it from the result;
    /// removing from that second source restores it.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the key is now held by exactly one source, an <b>Add</b> is emitted. If adding causes the count to reach 2+, a <b>Remove</b> is emitted (the item is no longer exclusive).</description></item>
    /// <item><term>Update</term><description>If the item is currently downstream (count is 1), an <b>Update</b> is emitted.</description></item>
    /// <item><term>Remove</term><description>Reference count decremented. If the count drops to exactly 1, an <b>Add</b> is emitted (the item is now exclusive to one source). If it drops to 0, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the item is downstream, a <b>Refresh</b> is forwarded.</description></item>
    /// <item><term>OnError</term><description>An error from any source terminates the combined output.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when all sources have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <seealso cref="And{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Or{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Except{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
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

    /// <inheritdoc cref="Xor{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <param name="sources"><see cref="ICollection{T}"/> a fixed collection of changeset streams to combine.</param>
    /// <remarks>This overload accepts a pre-built collection of sources instead of a params array.</remarks>
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
    /// <param name="sources"><see cref="ICollection{T}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
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
    /// <param name="sources"><see cref="IObservable{{T}}"/> the source collection of changeset streams.</param>
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

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).AsObservableChangeSet(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).ToObservableChangeSet<TCollection, TDestination>().AddKey(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).Connect();
}
