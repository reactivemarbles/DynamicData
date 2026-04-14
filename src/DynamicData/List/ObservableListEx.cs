// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static class ObservableListEx
{
    /// <summary>
    /// Injects a side effect into a changeset stream via an <see cref="IChangeSetAdaptor{T}"/>.
    /// The adaptor's <c>Adapt</c> method is called for each changeset under a synchronized lock, then the changeset is forwarded downstream.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="adaptor">The <see cref="IChangeSetAdaptor{T}"/> adaptor whose <c>Adapt</c> method is invoked for each changeset.</param>
    /// <returns>A list changeset stream identical to the source, with the adaptor side effect applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="adaptor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This operator synchronizes access with a lock, then calls <c>adaptor.Adapt(changes)</c> before forwarding the changeset.
    /// It is the primary extension point for custom UI binding adaptors (e.g., <see cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// delegates to this operator).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange/Replace/Remove/RemoveRange/Moved/Refresh/Clear</term><description>The adaptor's <c>Adapt</c> method is called with the full changeset, then it is forwarded downstream unchanged.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer. If the adaptor throws, the exception propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    public static IObservable<IChangeSet<T>> Adapt<T>(this IObservable<IChangeSet<T>> source, IChangeSetAdaptor<T> adaptor)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                return source.Synchronize(locker).Select(
                    changes =>
                    {
                        adaptor.Adapt(changes);
                        return changes;
                    }).SubscribeSafe(observer);
            });
    }

    /// <summary>
    /// Adds a key to each item in a list changeset, converting it to a cache changeset that supports all keyed DynamicData operators.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> function to extract a unique key from each item.</param>
    /// <returns>A cache changeset stream (<see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>) with keyed items.</returns>
    /// <remarks>
    /// <para>
    /// All index information is dropped during conversion because cache changesets are unordered by default.
    /// Use this when you need to transition from list-based pipelines to cache-based operators (Filter by key, Join, Group, etc.).
    /// </para>
    /// </remarks>
    /// <seealso cref="ObservableCacheEx.RemoveKey{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
    }

    /// <summary>
    /// Applies a logical AND (intersection) between multiple list changeset streams.
    /// Only items present in ALL sources appear in the result.
    /// </summary>
    /// <typeparam name="T">The type of items in the lists.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{T}}"/> changeset streams to intersect with.</param>
    /// <returns>A list changeset stream containing items that exist in every source.</returns>
    /// <remarks>
    /// <para>
    /// Uses reference counting per item across all sources. An item appears downstream only when
    /// its reference count is non-zero in ALL sources. Item identity is determined by the default equality comparer.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>The item's reference count is incremented in its source tracker. If the item is now present in all sources, an <b>Add</b> is emitted.</description></item>
    /// <item><term>Replace</term><description>The old item is removed from the tracker and the new item is added. Membership is recalculated for both.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>The item's reference count is decremented. If it was in the result and is no longer in all sources, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently in the result.</description></item>
    /// <item><term>Moved</term><description>Ignored (set operations are position-independent).</description></item>
    /// <item><term>OnError</term><description>Forwarded from any source.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when all sources complete.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Item identity uses object equality, not position. Duplicate items in a single source are reference-counted independently.</para>
    /// </remarks>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.And"/>
    public static IObservable<IChangeSet<T>> And<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.And, others);
    }

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">A <see cref="ICollection{T}"/> of changeset streams to intersect.</param>
    /// <remarks>This overload accepts a pre-built collection of sources instead of params array.</remarks>
    public static IObservable<IChangeSet<T>> And<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{T}"/> of changeset streams. Sources can be added or removed dynamically.</param>
    /// <remarks>This overload supports dynamic source management: adding or removing changeset streams from the observable list triggers re-evaluation.</remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{IObservableList{T}}"/> of <see cref="IObservableList{IObservableList{T}}"/>. Each inner list's changes are connected automatically.</param>
    /// <remarks>This overload accepts <see cref="IObservableList{T}"/> instances directly, calling <c>Connect()</c> internally.</remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{ISourceList{T}}"/> of <see cref="ISourceList{T}"/>. Each inner list's changes are connected automatically.</param>
    /// <remarks>This overload accepts <see cref="ISourceList{T}"/> instances directly, calling <c>Connect()</c> internally.</remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Wraps a <see cref="ISourceList{T}"/> as a read-only <see cref="IObservableList{T}"/>, hiding mutation methods.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> mutable source list to wrap.</param>
    /// <returns>A read-only observable list that mirrors the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a read-only <see cref="IObservableList{T}"/>.
    /// The list is kept in sync with the source stream for the lifetime of the subscription.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A read-only observable list reflecting the current state of the stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsObservableList{T}(ISourceList{T})"/>
    public static IObservableList<T> AsObservableList<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Monitors all properties on each item (via <see cref="INotifyPropertyChanged"/>) and emits <b>Refresh</b>
    /// changes when any property changes, causing downstream operators to re-evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of items, which must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch multiple refresh signals into a single changeset.</param>
    /// <param name="propertyChangeThrottle">An optional <see cref="TimeSpan"/> throttle applied to each item's property change notifications.</param>
    /// <param name="scheduler">The scheduler for throttle and buffer timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>A list changeset stream with additional <b>Refresh</b> changes injected when properties change.</returns>
    /// <remarks>
    /// <para>
    /// Wraps <see cref="AutoRefreshOnObservable{TObject, TAny}"/> using <c>WhenAnyPropertyChanged()</c> as the re-evaluator.
    /// Pair with <see cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/> or <see cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    /// to get reactive re-evaluation on property changes.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Subscribes to <c>PropertyChanged</c> on each new item. The original change is forwarded.</description></item>
    /// <item><term>Replace</term><description>Unsubscribes from the old item, subscribes to the new. The original change is forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>Unsubscribes from removed items. The original change is forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>Forwarded unchanged.</description></item>
    /// <item><term>Property changes</term><description>A <b>Refresh</b> change is emitted for the item whose property changed.</description></item>
    /// <item><term>OnError</term><description>Forwarded from the source or from the property change observable.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Each item generates a subscription. For large lists with frequent property changes, use <paramref name="changeSetBuffer"/> and <paramref name="propertyChangeThrottle"/> to reduce churn.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="AutoRefreshOnObservable{TObject, TAny}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{TAny}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.AutoRefresh"/>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            t =>
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

    /// <inheritdoc cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <typeparam name="TObject">The type of items in the list. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TProperty">The type of the monitored property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertyAccessor">An <see cref="Expression{TDelegate}"/> expression selecting the specific property to monitor.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch refresh signals.</param>
    /// <param name="propertyChangeThrottle">An optional <see cref="TimeSpan"/> throttle per item's property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for throttle and buffer timing.</param>
    /// <remarks>This overload monitors a single property instead of all properties. More efficient when only one property affects downstream operators.</remarks>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertyAccessor, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.AutoRefreshOnObservable(
            t =>
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
    /// Monitors each item with a custom observable and emits <b>Refresh</b> changes whenever that observable fires,
    /// causing downstream operators (Filter, Sort, Group) to re-evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TAny">The type emitted by the re-evaluator observable (value is ignored).</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="reevaluator">A <see cref="Func{T, TResult}"/> factory that, given an item, returns an observable whose emissions trigger a <b>Refresh</b> for that item.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch refresh signals into a single changeset.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for buffering.</param>
    /// <returns>A list changeset stream with additional <b>Refresh</b> changes injected when per-item observables fire.</returns>
    /// <remarks>
    /// <para>
    /// This is the general-purpose refresh mechanism. <see cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// is a convenience wrapper that uses <c>WhenAnyPropertyChanged()</c> as the re-evaluator.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Subscribes to the re-evaluator observable for each new item via <c>MergeMany</c>. The original change is forwarded.</description></item>
    /// <item><term>Replace</term><description>Unsubscribes from the old item's observable, subscribes to the new. The original change is forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>Unsubscribes from removed items. The original change is forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>Forwarded unchanged.</description></item>
    /// <item><term>Re-evaluator fires</term><description>The item's current index is looked up and a <b>Refresh</b> change is emitted.</description></item>
    /// <item><term>OnError</term><description>Forwarded from source or from any re-evaluator observable.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The internal index lookup (to find where each item is for the Refresh change) requires maintaining a cloned list, adding memory overhead proportional to list size.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.AutoRefreshOnObservable"/>
    public static IObservable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return new AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }

    /// <summary>
    /// Applies changeset mutations to a target <see cref="IObservableCollection{T}"/> for UI data binding.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="targetCollection">The <see cref="IObservableCollection{T}"/> target collection to keep in sync.</param>
    /// <param name="resetThreshold">When a changeset exceeds this many changes, the collection is reset instead of applying individual changes.</param>
    /// <returns>A continuation of the source changeset stream (allows further chaining).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="targetCollection"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Delegates to <see cref="Adapt{T}(IObservable{IChangeSet{T}}, IChangeSetAdaptor{T})"/> with an an internal collection adaptor.
    /// Each changeset is applied to the target collection on the calling thread. For UI binding, ensure the source is
    /// observed on the UI thread (e.g., via <c>ObserveOn</c>).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item inserted at the specified index in the target collection.</description></item>
    /// <item><term>AddRange</term><description>Items inserted as a range. If the count exceeds <paramref name="resetThreshold"/>, the collection is cleared and repopulated.</description></item>
    /// <item><term>Replace</term><description>Item at the specified index is replaced.</description></item>
    /// <item><term>Remove</term><description>Item at the specified index is removed.</description></item>
    /// <item><term>RemoveRange/Clear</term><description>Items removed from the collection.</description></item>
    /// <item><term>Moved</term><description>Item is moved between positions in the collection.</description></item>
    /// <item><term>Refresh</term><description>Depends on the adaptor implementation.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, out ReadOnlyObservableCollection{T}, int)"/>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    /// <seealso cref="Adapt{T}(IObservable{IChangeSet{T}}, IChangeSetAdaptor{T})"/>
    /// <seealso cref="ObservableCacheEx.Bind"/>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(targetCollection, options);
    }

    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="targetCollection">The <see cref="IObservableCollection{T}"/> target collection to keep in sync.</param>
    /// <param name="options"><see cref="BindingOptions"/> options controlling reset threshold and other behaviors.</param>
    /// <remarks>This overload accepts a <see cref="BindingOptions"/> struct for fine-grained control over binding behavior.</remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, options);
        return source.Adapt(adaptor);
    }

    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="readOnlyObservableCollection">An output parameter receiving the created <see cref="ReadOnlyObservableCollection{T}"/>.</param>
    /// <param name="resetThreshold">When a changeset exceeds this many changes, the collection is reset.</param>
    /// <remarks>This overload creates a <see cref="ReadOnlyObservableCollection{T}"/> via an <c>out</c> parameter, backed by an internal <c>ObservableCollectionExtended</c>.</remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="readOnlyObservableCollection">An output parameter receiving the created <see cref="ReadOnlyObservableCollection{T}"/>.</param>
    /// <param name="options"><see cref="BindingOptions"/> options controlling reset threshold and other behaviors.</param>
    /// <remarks>This overload creates a <see cref="ReadOnlyObservableCollection{T}"/> with <see cref="BindingOptions"/> for fine-grained control.</remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var target = new ObservableCollectionExtended<T>();
        var result = new ReadOnlyObservableCollection<T>(target);
        var adaptor = new ObservableCollectionAdaptor<T>(target, options);
        readOnlyObservableCollection = result;
        return source.Adapt(adaptor);
    }

#if SUPPORTS_BINDINGLIST

    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="bindingList">The target <see cref="BindingList{T}"/> to keep in sync.</param>
    /// <param name="resetThreshold">When a changeset exceeds this many changes, the list is reset.</param>
    /// <remarks>This overload binds to a <see cref="BindingList{T}"/> (WinForms binding). Uses a an internal binding list adaptor internally.</remarks>
    public static IObservable<IChangeSet<T>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this IObservable<IChangeSet<T>> source, BindingList<T> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

        return source.Adapt(new BindingListAdaptor<T>(bindingList, resetThreshold));
    }

#endif

    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload starts unpaused and has no timeout.</remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, scheduler);

    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload allows setting the initial pause state but has no timeout.</remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return BufferIf(source, pauseIfTrueSelector, initialPauseState, null, scheduler);
    }

    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload starts unpaused and accepts a timeout but not an explicit initial pause state.</remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

    /// <summary>
    /// Buffers changeset notifications while a pause signal is active, then flushes all buffered changes when resumed.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{bool}"/> of <see cref="bool"/> that controls buffering: <see langword="true"/> pauses (buffers), <see langword="false"/> resumes (flushes).</param>
    /// <param name="initialPauseState">The initial pause state. When <see langword="true"/>, buffering starts immediately.</param>
    /// <param name="timeOut">An optional <see cref="TimeSpan"/> maximum duration to keep the buffer open. After this time, the buffer is flushed regardless of pause state.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for timeout scheduling.</param>
    /// <returns>A list changeset stream that buffers during pause and emits combined changesets on resume.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pauseIfTrueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All changeset events are buffered at the changeset level (not individual changes) while paused.
    /// On resume, all buffered changesets are emitted as a single combined changeset. If the buffer is empty on resume,
    /// no emission occurs.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Any (while paused)</term><description>Accumulated in an internal buffer. Not emitted downstream.</description></item>
    /// <item><term>Any (while active)</term><description>Passed through immediately.</description></item>
    /// <item><term>Pause selector emits false</term><description>All buffered changesets are flushed downstream as one combined changeset.</description></item>
    /// <item><term>Timeout fires</term><description>Automatically resumes and flushes the buffer.</description></item>
    /// <item><term>OnError</term><description>Forwarded immediately (not buffered).</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded immediately.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Each pause/resume cycle re-arms the timeout. Rapid toggling can create many small buffer windows.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return new BufferIf<T>(source, pauseIfTrueSelector, initialPauseState, timeOut, scheduler).Run();
    }

    /// <summary>
    /// Buffers changesets during an initial time window, then emits a single combined changeset and passes through subsequent changes.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="initialBuffer">The <see cref="TimeSpan"/> time period (measured from first emission) during which changes are buffered.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for timing the buffer window.</param>
    /// <returns>A list changeset stream where the initial burst is combined into one changeset.</returns>
    /// <remarks>
    /// <para>
    /// Composed from <see cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>, <c>Buffer</c>, and <see cref="FlattenBufferResult{T}"/>.
    /// After the initial buffer period, all subsequent changesets pass through immediately.
    /// </para>
    /// </remarks>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="FlattenBufferResult{T}(IObservable{IList{IChangeSet{T}}})"/>
    public static IObservable<IChangeSet<TObject>> BufferInitial<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
        where TObject : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

    /// <summary>
    /// Casts each item in the changeset from <c>object</c> to <typeparamref name="TDestination"/> using a direct cast.
    /// </summary>
    /// <typeparam name="TDestination">The target type to cast to.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{object}}"/> of <see cref="IChangeSet{T}"/>. of <c>object</c> items.</param>
    /// <returns>A list changeset stream of cast items.</returns>
    /// <seealso cref="Cast{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination})"/>
    /// <seealso cref="CastToObject{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TDestination>(this IObservable<IChangeSet<object>> source)
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => changes.Transform(t => (TDestination)t));
    }

    /// <summary>
    /// Transforms each item in the changeset using a conversion function.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDestination">The destination item type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="conversionFactory">A <see cref="Func{T, TResult}"/> function to convert each item from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.</param>
    /// <returns>A list changeset stream of converted items.</returns>
    /// <remarks>Use this overload when type inference requires explicit specification of both source and destination types. Alternatively, call <see cref="CastToObject{T}"/> first, then the single-type-parameter <see cref="Cast{TDestination}"/> overload.</remarks>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Casts each item in the changeset to <c>object</c>. Typically used before <see cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/> to work around type inference limitations.
    /// </summary>
    /// <typeparam name="T">The source item type (must be a reference type).</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream of <c>object</c> items.</returns>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    public static IObservable<IChangeSet<object>> CastToObject<T>(this IObservable<IChangeSet<T>> source)
        where T : class => source.Select(changes => changes.Transform(t => (object)t));

    /// <summary>
    /// Applies each changeset to the target list as a side effect, keeping it synchronized with the source.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="target">The <see cref="IList{T}"/> target list to clone changes into.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Lower-level than <see cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>. Uses <see cref="IList{T}"/>.Clone() to apply all changeset operations directly.</para>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <seealso cref="PopulateInto{T}(IObservable{IChangeSet{T}}, ISourceList{T})"/>
    public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Do(target.Clone);
    }

    /// <summary>
    /// <para>Convert the object using the specified conversion function.</para>
    /// <para>This is a lighter equivalent of Transform and is designed to be used with non-disposable objects.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="conversionFactory">The <see cref="Func{T, TResult}"/> conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    [Obsolete("Prefer Cast as it is does the same thing but is semantically correct")]
    public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Defers downstream delivery until the source emits its first changeset, then forwards all subsequent changesets.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream that begins emitting only after the source has produced its first changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Subscribes to the source immediately but buffers internally until the first changeset arrives, at which point it emits
    /// the initial data and all subsequent changesets. This is useful when downstream consumers should not receive an empty initial state.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>First changeset</term><description>Delivered downstream, unlocking the stream for all future emissions.</description></item>
    /// <item><term>Subsequent changesets</term><description>Forwarded immediately.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<T>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <remarks>
    /// <para>Convenience overload that calls <c>source.Connect().DeferUntilLoaded()</c>.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().DeferUntilLoaded();
    }

    /// <summary>
    /// Disposes items that implement <see cref="IDisposable"/> when they are removed, replaced, or cleared from the stream.
    /// All remaining tracked items are disposed when the stream finalizes (OnCompleted, OnError, or subscription disposal).
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A continuation of the source changeset stream with disposal side effects applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Items are cast to <see cref="IDisposable"/> and disposed after the changeset has been forwarded downstream.
    /// Items that do not implement <see cref="IDisposable"/> are silently ignored.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Items are tracked for future disposal. Changeset forwarded.</description></item>
    /// <item><term><b>Replace</b></term><description>The previous (replaced) item is disposed after the changeset is forwarded. The new item is tracked.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>Removed items are disposed after the changeset is forwarded.</description></item>
    /// <item><term><b>Clear</b></term><description>All tracked items are disposed after the changeset is forwarded.</description></item>
    /// <item><term><b>Moved</b>/<b>Refresh</b></term><description>Forwarded. No disposal occurs.</description></item>
    /// <item><term>OnError/OnCompleted/Disposal</term><description>All remaining tracked items are disposed during finalization.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Disposal happens after the changeset is delivered downstream, so subscribers see the change before items are disposed.</para>
    /// </remarks>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/>
    /// <seealso cref="ObservableCacheEx.DisposeMany"/>
    public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<T>(source).Run();
    }

    /// <summary>
    /// Extracts distinct values from source items using <paramref name="valueSelector"/>, with reference counting to track when values enter and leave the result set.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TValue">The type of distinct values produced.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}"/> function that extracts the value to track from each source item.</param>
    /// <returns>A list changeset stream of distinct values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains an internal reference count per distinct value. A value is included when its count first exceeds zero
    /// and removed when its count drops back to zero.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Value extracted. If first occurrence, an <b>Add</b> is emitted. Otherwise the reference count is incremented silently.</description></item>
    /// <item><term><b>Replace</b></term><description>Old value's reference count decremented (removed if zero), new value's count incremented (added if first). If the value did not change, no emission.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>Reference count decremented. If the count reaches zero, a <b>Remove</b> is emitted for that distinct value.</description></item>
    /// <item><term><b>Refresh</b></term><description>Value is re-extracted. If changed, old value decremented and new value incremented (same as Replace logic).</description></item>
    /// <item><term><b>Clear</b></term><description>All reference counts cleared. <b>Remove</b> emitted for every tracked distinct value.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// <seealso cref="ObservableCacheEx.DistinctValues"/>
    public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return new Distinct<TObject, TValue>(source, valueSelector).Run();
    }

    /// <summary>
    /// Applies a logical set-difference (Except) between the source and other streams.
    /// Items present in the first source but not in any of the <paramref name="others"/> are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to exclude from the result.</param>
    /// <returns>A list changeset stream containing items from <paramref name="source"/> that are not in any of <paramref name="others"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Uses reference-counted equality comparison across all sources. Items are compared by equality (not index position).
    /// The first source has a special role: only items from it can appear in the result, and only if they do not exist in any other source.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b> (first source)</term><description>If the item does not exist in any other source, an <b>Add</b> is emitted.</description></item>
    /// <item><term><b>Add</b>/<b>AddRange</b> (other source)</term><description>If the item was in the result (from first source), a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (first source)</term><description>If the item was in the result, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (other source)</term><description>If the item exists in the first source and no longer in any other, an <b>Add</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Treated as a Remove of the old item plus an Add of the new item, with set logic re-evaluated.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored by the set logic (no positional semantics).</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is currently in the result set.</description></item>
    /// <item><term>OnError</term><description>Forwarded from any source.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when all sources have completed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Unlike <see cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>, the first source is asymmetric: only its items can appear in the result.</para>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.Except"/>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Except, others);
    }

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <para>Static overload accepting a pre-built collection of sources. The first item in the collection is the primary source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <para>Dynamic overload: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime. The first source in the list acts as the primary.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <para>Dynamic overload accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <para>Dynamic overload accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Automatically removes items from the <paramref name="source"/> list after the duration returned by <paramref name="timeSelector"/>.
    /// Returns an observable of the items that were expired and removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> source list to monitor and remove expired items from.</param>
    /// <param name="timeSelector">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for items that should never expire.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> polling interval to batch expiry checks. If omitted, a separate timer is created for each unique expiry time.</param>
    /// <param name="scheduler">The scheduler for scheduling expiry timers. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time expired items are removed from the source list.</returns>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <see cref="ISourceList{T}"/>, not on a changeset stream. It monitors items as they are added,
    /// schedules their removal, and physically removes them from the source list when their time expires.
    /// </para>
    /// <para>
    /// When <paramref name="pollingInterval"/> is specified, all items due for removal are batched into a single removal at each polling tick,
    /// which can improve performance when many items expire around the same time.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the expired items (not changesets). Subscribe to this observable to trigger the expiry mechanism; if not subscribed, no items will be removed.</para>
    /// </remarks>
    /// <seealso cref="LimitSizeTo{T}(ISourceList{T}, int, IScheduler?)"/>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{T}, Func{T, TimeSpan?}, IScheduler?)"/>
    public static IObservable<IEnumerable<T>> ExpireAfter<T>(
                this ISourceList<T> source,
                Func<T, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval = null,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ExpireAfter<T>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Filters items from the source list changeset stream using a static predicate.
    /// Only items satisfying <paramref name="predicate"/> are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>. to filter.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate that determines which items are included. Items returning <see langword="true"/> appear downstream; items returning <see langword="false"/> are excluded.</param>
    /// <returns>A list changeset stream containing only items that satisfy <paramref name="predicate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Use this overload when the predicate is fixed for the lifetime of the subscription. Item ordering is preserved.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If the item passes, an <b>Add</b> is emitted at the calculated downstream index. Otherwise dropped.</description></item>
    /// <item><term>AddRange</term><description>Each item in the range is evaluated. Matching items are emitted as an <b>AddRange</b>.</description></item>
    /// <item><term>Replace</term><description>The predicate is re-evaluated. Four outcomes: both pass produces <b>Replace</b>; new passes but old didn't produces <b>Add</b>; old passed but new doesn't produces <b>Remove</b>; neither passes is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>RemoveRange</term><description>Included items in the range are emitted as individual <b>Remove</b> changes.</description></item>
    /// <item><term>Refresh</term><description>The predicate is re-evaluated. If the item now passes but previously did not, an <b>Add</b> is emitted. If it previously passed but no longer does, a <b>Remove</b> is emitted. If still passes, the <b>Refresh</b> is forwarded. If still fails, dropped.</description></item>
    /// <item><term>Clear</term><description>All downstream items are cleared.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Refresh events trigger re-evaluation, which can promote or demote items (turning a Refresh into an Add or Remove). Pair with <see cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/> for property-change-driven filtering.</para>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    /// <seealso cref="FilterOnObservable{TObject}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.Filter"/>
    public static IObservable<IChangeSet<T>> Filter<T>(
                this IObservable<IChangeSet<T>> source,
                Func<T, bool> predicate)
            where T : notnull
        => List.Internal.Filter.Static<T>.Create(
            source: source,
            predicate: predicate,
            suppressEmptyChangesets: true);

    /// <summary>
    /// Filters items using a dynamically changing predicate.
    /// When <paramref name="predicate"/> emits a new function, all items are re-evaluated.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="predicate">An <see cref="IObservable{Func{T, bool}}"/> that emits new predicate functions. Each emission triggers a full re-evaluation of all items.</param>
    /// <param name="filterPolicy">The <see cref="ListFilterPolicy"/> that controls re-filtering behavior: <see cref="ListFilterPolicy.CalculateDiff"/> (default) computes the minimal diff between old and new results; <see cref="ListFilterPolicy.ClearAndReplace"/> clears and repopulates entirely.</param>
    /// <returns>A list changeset stream containing only items that satisfy the most recent predicate.</returns>
    /// <remarks>
    /// <para>
    /// Each time <paramref name="predicate"/> emits, every item is re-evaluated. The <paramref name="filterPolicy"/> controls
    /// whether this produces a minimal diff (Add/Remove for items that changed status) or a full Clear+AddRange.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The current predicate is evaluated. If the item passes, an <b>Add</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>AddRange</term><description>Each item is evaluated. Matching items are emitted as <b>AddRange</b>.</description></item>
    /// <item><term>Replace</term><description>Re-evaluated. Same four-outcome logic as the static overload (Replace, Add, Remove, or dropped).</description></item>
    /// <item><term>Remove</term><description>If the item was downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluated. If inclusion status changed, an <b>Add</b> or <b>Remove</b> is emitted. If unchanged, <b>Refresh</b> forwarded or dropped.</description></item>
    /// <item><term>Clear</term><description>All downstream items are cleared.</description></item>
    /// <item><term>Predicate changed</term><description>All items re-evaluated against the new predicate. With <see cref="ListFilterPolicy.CalculateDiff"/>, only items that changed status emit Add/Remove. With <see cref="ListFilterPolicy.ClearAndReplace"/>, a Clear is emitted followed by AddRange of all matching items.</description></item>
    /// <item><term>OnError</term><description>Forwarded from the source or from <paramref name="predicate"/>.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes. Independent completion of <paramref name="predicate"/> does not terminate the filter.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No items are included until <paramref name="predicate"/> emits its first function. <see cref="ListFilterPolicy.CalculateDiff"/> is generally preferred for performance; <see cref="ListFilterPolicy.ClearAndReplace"/> is useful when downstream consumers (like UI bindings) handle full resets more efficiently than individual changes.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="FilterOnObservable{TObject}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, IObservable<Func<T, bool>> predicate, ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new List.Internal.Filter.Dynamic<T>(source, predicate, filterPolicy).Run();
    }

    /// <summary>
    /// Filters items using a predicate that receives external state. When <paramref name="predicateState"/> emits a new state value,
    /// all items are re-evaluated against <paramref name="predicate"/> using the updated state.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="TState">The type of state value required by <paramref name="predicate"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="predicateState">An <see cref="IObservable{TState}"/> stream of state values to be passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate receiving the current state and an item, returning <see langword="true"/> to include or <see langword="false"/> to exclude.</param>
    /// <param name="filterPolicy">The <see cref="ListFilterPolicy"/> that controls re-filtering behavior: <see cref="ListFilterPolicy.CalculateDiff"/> (default) computes minimal diff; <see cref="ListFilterPolicy.ClearAndReplace"/> clears and repopulates.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed. Set to <see langword="false"/> to publish empty changesets (useful for monitoring loading status).</param>
    /// <returns>A list changeset stream containing only items satisfying <paramref name="predicate"/> with the current state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="predicateState"/>, or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The predicate cannot be invoked until the first state value is received. Until then, all items are treated as excluded.
    /// Each subsequent state emission triggers a full re-evaluation of all items according to <paramref name="filterPolicy"/>.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Evaluated using current state. Matching items emitted as <b>Add</b>/<b>AddRange</b>.</description></item>
    /// <item><term><b>Replace</b></term><description>Re-evaluated. Same four-outcome logic as the static filter (Replace, Add, Remove, or dropped).</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>If the item was downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b></term><description>Re-evaluated against current state. Inclusion status may change.</description></item>
    /// <item><term><b>Clear</b></term><description>All downstream items are cleared.</description></item>
    /// <item><term>State changed</term><description>All items re-evaluated with new state value. <see cref="ListFilterPolicy.CalculateDiff"/> emits minimal Add/Remove; <see cref="ListFilterPolicy.ClearAndReplace"/> emits Clear then AddRange.</description></item>
    /// <item><term>OnError</term><description>Forwarded from the source or from <paramref name="predicateState"/>.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <paramref name="predicateState"/> should emit an initial value immediately upon subscription. No items are included until the first state value arrives.</para>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    public static IObservable<IChangeSet<T>> Filter<T, TState>(
                this IObservable<IChangeSet<T>> source,
                IObservable<TState> predicateState,
                Func<TState, T, bool> predicate,
                ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff,
                bool suppressEmptyChangeSets = true)
            where T : notnull
        => List.Internal.Filter.WithPredicateState<T, TState>.Create(
            source: source,
            predicateState: predicateState,
            predicate: predicate,
            filterPolicy: filterPolicy,
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Filters each item using a per-item <see cref="IObservable{T}"/> of <see cref="bool"/> that dynamically controls inclusion.
    /// When an item's observable emits <see langword="true"/> the item enters the result; when it emits <see langword="false"/> the item is removed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="objectFilterObservable">A function that returns an observable of <see cref="bool"/> for each item, controlling its inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration applied to each per-item observable to reduce re-evaluation frequency.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling. Defaults to the system default scheduler.</param>
    /// <returns>A list changeset stream containing only items whose per-item observable most recently emitted <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="objectFilterObservable"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each item in the source gets its own subscription to the observable returned by <paramref name="objectFilterObservable"/>.
    /// The item's inclusion is determined by the most recent boolean value emitted by that observable.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Item is included when it first emits <see langword="true"/>.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed. If the item was downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is currently included.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <list type="table">
    /// <listheader><term>Event (per-item observable)</term><description>Behavior</description></listheader>
    /// <item><term>Emits <see langword="true"/></term><description>If not already included, an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term>Emits <see langword="false"/></term><description>If currently included, a <b>Remove</b> is emitted downstream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.FilterOnObservable"/>
    public static IObservable<IChangeSet<TObject>> FilterOnObservable<TObject>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> objectFilterObservable, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new FilterOnObservable<TObject>(source, objectFilterObservable, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Filters items based on a property value, automatically re-evaluating when the specified property changes on any item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property to monitor for changes.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate evaluated against the item to determine inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of items satisfying the predicate, re-evaluated on property changes.</returns>
    /// <remarks>
    /// <para>Deprecated. Use <see cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/> followed by <see cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/> instead.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Flattens a buffered list of changesets (from Rx's <c>Buffer</c> operator) back into a single changeset stream.
    /// Empty buffers are dropped.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="IObservable{T}"/> of buffered changeset lists.</param>
    /// <returns>A list changeset stream with all buffered changes concatenated into single changesets.</returns>
    /// <remarks>
    /// <para>Use this after applying <c>Observable.Buffer()</c> to a changeset stream to re-merge the batched changesets into a single stream.</para>
    /// </remarks>
    /// <seealso cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, IScheduler?)"/>
    /// <seealso cref="BufferInitial{T}(IObservable{IChangeSet{T}}, TimeSpan, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        where T : notnull => source.Where(x => x.Count != 0).Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));

    /// <summary>
    /// Invokes <paramref name="action"/> for every <see cref="Change{T}"/> in each changeset, including range changes as-is.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="action">The action invoked for each <see cref="Change{T}"/>. Range changes (AddRange, RemoveRange, Clear) are received as a single <see cref="Change{T}"/> with a populated <c>Range</c> property.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a side-effect operator. It does not modify the changeset. If you need each individual item from range operations flattened out, use <see cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/> instead.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/Replace/Remove/Moved/Refresh</term><description>Callback invoked with the <see cref="Change{T}"/> (single-item change). Changeset forwarded.</description></item>
    /// <item><term>AddRange/RemoveRange/Clear</term><description>Callback invoked once with the <see cref="Change{T}"/> containing the range (accessible via <c>Range</c> property). Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>Forwarded. If callback throws, propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="ObservableCacheEx.ForEachChange"/>
    public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<Change<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.ForEach(action));
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every individual <see cref="ItemChange{TObject}"/> in each changeset.
    /// Range changes are flattened into individual item changes first, so the callback only receives Add, Replace, Remove, and Refresh.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="action">The <see cref="Action{ItemChange{TObject}}"/> action invoked for each individual item change.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>, this operator flattens
    /// <b>AddRange</b>, <b>RemoveRange</b>, and <b>Clear</b> into individual <see cref="ItemChange{TObject}"/> entries before invoking the callback.
    /// </para>
    /// </remarks>
    /// <seealso cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>
    public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<ItemChange<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.Flatten().ForEach(action));
    }

    /// <summary>
    /// Groups source items by the value returned by <paramref name="groupSelector"/>. Each group is an <see cref="IGroup{TObject, TGroup}"/>
    /// containing an inner observable list of its members.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="groupSelector">A <see cref="Func{T, TResult}"/> function that returns the group key for each item.</param>
    /// <param name="regrouper">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> that forces all items to be re-evaluated against <paramref name="groupSelector"/> when it fires. Useful for time-based groupings (e.g., "Last Hour", "Today").</param>
    /// <returns>A list changeset stream of <see cref="IGroup{TObject, TGroup}"/> objects, each containing the items belonging to that group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="groupSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Groups are created lazily and removed when empty. Each group exposes an inner observable list that receives incremental updates.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Group key evaluated. Item added to its group. If the group is new, an <b>Add</b> of the group is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Group key re-evaluated. If the group changed, the item is removed from the old group and added to the new one. Empty old groups are removed.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Item removed from its group. Empty groups are removed from the result.</description></item>
    /// <item><term><b>Refresh</b></term><description>Group key re-evaluated. If changed, the item moves between groups.</description></item>
    /// <item><term><b>Moved</b></term><description>Not handled by group logic.</description></item>
    /// <item><term>Regrouper fires</term><description>All items re-evaluated. Items that changed group key are moved between groups. Empty groups removed, new groups added.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOn<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroup> groupSelector, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelector.ThrowArgumentNullExceptionIfNull(nameof(groupSelector));

        return new GroupOn<TObject, TGroup>(source, groupSelector, regrouper).Run();
    }

    /// <summary>
    /// Groups items by a property value, automatically re-grouping when the specified property changes on any item.
    /// Each group contains an inner observable list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property whose value determines the group key.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of <see cref="IGroup{TObject, TGroup}"/> objects.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Convenience operator equivalent to <c>.AutoRefresh(propertySelector).GroupOn(item => property)</c>.
    /// Property changes trigger re-evaluation of the group key, potentially moving items between groups.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOnProperty<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnProperty<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Groups items by a property value, automatically re-grouping when the specified property changes.
    /// Each group emits immutable snapshots (not live observable lists).
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property whose value determines the group key.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of <see cref="List.IGrouping{TObject, TGroup}"/> immutable group snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Combines <see cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// with <see cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>.
    /// Unlike <see cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>,
    /// this produces immutable snapshots per group rather than live inner observable lists.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroup>>> GroupOnPropertyWithImmutableState<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnPropertyWithImmutableState<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Groups source items by the value returned by <paramref name="groupSelectorKey"/>. Each update produces immutable grouping snapshots
    /// rather than live inner observable lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> function that returns the group key for each item.</param>
    /// <param name="regrouper">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> that forces all items to be re-evaluated when it fires.</param>
    /// <returns>A list changeset stream of <see cref="List.IGrouping{TObject, TGroupKey}"/> immutable snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="groupSelectorKey"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Works like <see cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// but each affected group emits a new immutable snapshot on every change rather than updating a live inner list.
    /// This is useful when consumers need thread-safe, point-in-time snapshots of each group.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupOn{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroup}, IObservable{Unit}?)"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> GroupWithImmutableState<TObject, TGroupKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOnImmutable<TObject, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    /// <summary>
    /// Limits the source list to a maximum number of items using FIFO eviction.
    /// When the list exceeds <paramref name="sizeLimit"/>, the oldest items are removed.
    /// Returns an observable of the items that were removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> source list to monitor and evict items from.</param>
    /// <param name="sizeLimit">The maximum number of items allowed. Must be greater than zero.</param>
    /// <param name="scheduler">The scheduler for scheduling size checks. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time excess items are removed from the source list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sizeLimit"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <see cref="ISourceList{T}"/>. It subscribes to the source's changes,
    /// tracks insertion order using an internal Transform, and removes the oldest items when the size limit is exceeded.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the removed items (not changesets). Subscribe to this observable to activate the size-limiting mechanism. Removal is performed synchronously under a lock shared with the change tracking.</para>
    /// </remarks>
    /// <seealso cref="ExpireAfter{T}(ISourceList{T}, Func{T, TimeSpan?}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="Top{T}(IObservable{IChangeSet{T}}, int)"/>
    public static IObservable<IEnumerable<T>> LimitSizeTo<T>(this ISourceList<T> source, int sizeLimit, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));
        }

        var locker = InternalEx.NewLock();
        var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? GlobalConfig.DefaultScheduler, locker);

        return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
    }

    /// <summary>
    /// Subscribes to a per-item observable for each item in the source and merges all emissions into a single <see cref="IObservable{TDestination}"/> stream.
    /// This is NOT a changeset operator: it returns a flat observable of values.
    /// </summary>
    /// <typeparam name="T">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by per-item observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns an observable for each source item.</param>
    /// <returns>An observable that emits values from all per-item observables, merged together.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Internally uses <see cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/> to manage per-item subscriptions.
    /// When an item is added, a new subscription is created via <paramref name="observableSelector"/>. When removed or replaced, the old subscription is disposed.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Subscription behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed.</description></item>
    /// <item><term><b>Refresh</b>/<b>Moved</b></term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnCompleted (source)</term><description>Completes only after the source and all active inner observables have completed.</description></item>
    /// <item><term>OnError</term><description>Forwarded from the source or from any per-item observable.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="ObservableCacheEx.MergeMany"/>
    public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<T, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Merges multiple list changeset streams from an observable-of-observables into a single unified changeset stream.
    /// Unlike cache MergeChangeSets, list merging performs no key-based deduplication.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of nested changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used by the merge tracker to compare items.</param>
    /// <returns>A single list changeset stream containing all changes from all inner streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All changes from inner streams are forwarded to the output.
    /// <b>Replace</b> changes are decomposed into a Remove of the old item followed by an Add of the new item.
    /// <b>Moved</b> changes from inner streams are ignored.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Replace</b></term><description>The old value is replaced by the new value in the merged output. If the old value is not found (by reference), the new value is added instead.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// <item><term>OnError</term><description>Forwarded from any inner source.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when all inner sources have completed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> There is no key-based deduplication. If the same item appears in multiple inner streams, it will appear multiple times in the merged output.</para>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.MergeChangeSets"/>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer).Run();
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <summary>
    /// Merges two list changeset streams into a single unified stream.
    /// </summary>
    /// <param name="source">The first <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/> changeset stream.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/> changeset stream to merge with.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IObservable<IChangeSet<TObject>> other, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <summary>
    /// Merges the source list changeset stream with additional changeset streams into a single unified stream.
    /// </summary>
    /// <param name="source">The primary <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/> changeset stream.</param>
    /// <param name="others">The additional <see cref="IEnumerable{T}"/> of list changeset streams to merge with.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IEnumerable<IObservable<IChangeSet<TObject>>> others, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Merges a collection of list changeset streams into a single unified changeset stream.
    /// This is the primary overload that all other list MergeChangeSets overloads delegate to.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The <see cref="IEnumerable{T}"/> collection of list changeset streams to merge.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used by the merge tracker to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    /// <returns>A single list changeset stream containing all changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Replace</b> changes from inner streams are handled as a replace-or-add: if the old item is found in the merged output, it is replaced; otherwise the new item is added. <b>Moved</b> changes from inner streams are ignored.
    /// There is no key-based deduplication (unlike cache MergeChangeSets).
    /// </para>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer, completable, scheduler).Run();
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <summary>
    /// Merges list changeset streams from an <see cref="IObservableList{T}"/> into a single stream. Sources can be added or removed dynamically.
    /// </summary>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservableList<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <summary>
    /// Merges list changeset streams from a list-of-list-changeset-observables into a single stream.
    /// Each inner list changeset observable in the source list is merged, and parent item removal triggers child cleanup.
    /// </summary>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject>>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer);
    }

    /// <summary>
    /// Merges cache changeset streams from an <see cref="IObservableList{T}"/> into a single cache changeset stream.
    /// Uses <paramref name="comparer"/> to resolve conflicts when the same key appears in multiple child streams.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The <see cref="IObservableList{T}"/> of cache changeset observables.</param>
    /// <param name="comparer"><see cref="IComparer{TObject}"/> to resolve which value wins when the same key appears in multiple sources.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Sources can be added or removed dynamically from the observable list. Parent item removal triggers cleanup of all child items from that source.</para>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IEqualityComparer{TObject}?, IComparer{TObject}?)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from an <see cref="IObservableList{T}"/> into a single cache changeset stream, with optional equality and ordering comparers.
    /// </summary>
    /// <param name="source">The <see cref="IObservableList{T}"/> of cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TObject}"/> to resolve conflicts when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer, comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, using a comparer for conflict resolution.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{T}"/> whose items are cache changeset observables.</param>
    /// <param name="comparer"><see cref="IComparer{TObject}"/> to resolve which value wins when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeChangeSets(comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, with optional equality and ordering comparers.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{T}"/> whose items are cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TObject}"/> to resolve conflicts when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer, comparer);
    }

    /// <summary>
    /// Transforms each source item into a child list changeset stream using <paramref name="observableSelector"/>,
    /// then merges all child streams into a single flat list changeset stream. Parent item removal cleans up all associated children.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child list changeset stream for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> used to compare child items.</param>
    /// <returns>A single list changeset stream containing all items from all child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Internally subscribes to each child stream when a source item is added and disposes the subscription when it is removed.
    /// All child items from a removed parent are removed from the merged output.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the child stream. Child emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old child subscription disposed (and its items removed from output). New child subscription created.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Child subscription disposed. All child items from that parent are removed.</description></item>
    /// <item><term>OnError</term><description>Forwarded from the source or any child stream.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when the source completes.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <seealso cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="ObservableCacheEx.MergeManyChangeSets"/>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TDestination : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector == null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        return new MergeManyListChangeSets<TObject, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// Transforms each source item into a child cache changeset stream and merges all children into a single cache changeset stream.
    /// Uses <paramref name="comparer"/> to resolve key conflicts when the same key appears in multiple child streams.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child cache changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key in the child cache changesets.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child cache changeset stream for each source item.</param>
    /// <param name="comparer"><see cref="IComparer{TDestination}"/> to resolve which value wins when the same key appears from multiple children.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="comparer"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Delegates to <see cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> with a <see langword="null"/> equality comparer.</para>
    /// </remarks>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// Transforms each source item into a child cache changeset stream and merges all children into a single cache changeset stream.
    /// This is the primary list-to-cache MergeManyChangeSets overload.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child cache changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key in the child cache changesets.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child cache changeset stream for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TDestination}"/> to resolve conflicts when the same key appears from multiple children.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each source item produces a keyed child stream via <paramref name="observableSelector"/>. All child items are tracked by key.
    /// When a parent item is removed, all its child items are removed from the merged output.
    /// When the same key appears from multiple children, <paramref name="comparer"/> determines which value wins.
    /// </para>
    /// </remarks>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Suppresses empty changesets from the stream. Only changesets with at least one change are forwarded.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream with empty changesets filtered out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="WhereReasonsAre{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    public static IObservable<IChangeSet<T>> NotEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(s => s.Count != 0);
    }

    /// <summary>
    /// Invokes <paramref name="addAction"/> for every item added to the source list stream.
    /// Triggers on <see cref="ListChangeReason.Add"/>, <see cref="ListChangeReason.AddRange"/>, and the new item of <see cref="ListChangeReason.Replace"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="addAction">The <see cref="Action{T}"/> action to invoke for each added item.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="addAction"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The action fires before the changeset is forwarded downstream.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Callback invoked with the added item. Changeset forwarded.</description></item>
    /// <item><term>AddRange</term><description>Callback invoked for each item in the range. Changeset forwarded.</description></item>
    /// <item><term>Replace</term><description>Callback invoked for the <b>new</b> (replacement) item. Changeset forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>No callback. Changeset forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>No callback. Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>Forwarded. If callback throws, propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="OnItemRefreshed{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.OnItemAdded"/>
    public static IObservable<IChangeSet<T>> OnItemAdded<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> addAction)
            where T : notnull
        => List.Internal.OnItemAdded<T>.Create(
            source: source,
            addAction: addAction);

    /// <summary>
    /// Invokes <paramref name="refreshAction"/> for every item with a <see cref="ListChangeReason.Refresh"/> change in the source stream.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="refreshAction">The <see cref="Action{T}"/> action to invoke for each refreshed item.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="refreshAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.OnItemRefreshed"/>
    public static IObservable<IChangeSet<T>> OnItemRefreshed<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> refreshAction)
            where T : notnull
        => List.Internal.OnItemRefreshed<T>.Create(
            source: source,
            refreshAction: refreshAction);

    /// <summary>
    /// Invokes <paramref name="removeAction"/> for every item removed from the source list stream.
    /// Triggers on <see cref="ListChangeReason.Remove"/>, <see cref="ListChangeReason.RemoveRange"/>, <see cref="ListChangeReason.Clear"/>, and the old item of <see cref="ListChangeReason.Replace"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="removeAction">The <see cref="Action{T}"/> action to invoke for each removed item.</param>
    /// <param name="invokeOnUnsubscribe">When <see langword="true"/> (default), <paramref name="removeAction"/> is also invoked for all remaining tracked items upon stream disposal, completion, or error.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="removeAction"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// When <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>, the operator tracks all items that have been added but not yet removed,
    /// and fires <paramref name="removeAction"/> for each of them during finalization. This is useful for resource cleanup patterns.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Tracked internally (when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>). No callback invoked. Changeset forwarded.</description></item>
    /// <item><term>Replace</term><description>Callback invoked for the <b>previous</b> (replaced) item. New item tracked. Changeset forwarded.</description></item>
    /// <item><term>Remove</term><description>Callback invoked for the removed item. Changeset forwarded.</description></item>
    /// <item><term>RemoveRange/Clear</term><description>Callback invoked for each removed item. Changeset forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>No callback. Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>If <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>, callback is invoked for all tracked items before the error propagates.</description></item>
    /// <item><term>OnCompleted</term><description>If <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>, callback is invoked for all tracked items before completion propagates.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> When <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/> (the default), disposing the subscription also invokes the callback for every item still in the list, not just items that were explicitly removed during the subscription. Exceptions in <paramref name="removeAction"/> are not caught.</para>
    /// </remarks>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="DisposeMany{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/>
    /// <seealso cref="ObservableCacheEx.OnItemRemoved"/>
    public static IObservable<IChangeSet<T>> OnItemRemoved<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> removeAction,
                bool invokeOnUnsubscribe = true)
            where T : notnull
        => List.Internal.OnItemRemoved<T>.Create(
            source: source,
            removeAction: removeAction,
            invokeOnUnsubscribe: invokeOnUnsubscribe);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Applies a logical OR (union) between a pre-built collection of list changeset sources. Items present in any source are included.
    /// </summary>
    /// <seealso cref="ObservableCacheEx.Or"/>
    public static IObservable<IChangeSet<T>> Or<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Applies a logical OR (union) between the source and other list changeset streams.
    /// Items present in any of the sources are included in the result, using reference-counted equality.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to combine with.</param>
    /// <returns>A list changeset stream containing items that exist in at least one source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Uses reference-counted equality comparison. An item is included when it first appears in any source and removed when it no longer exists in any source.
    /// <b>Moved</b> changes are ignored by the set logic.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b> (any source)</term><description>If the item is new to the result, an <b>Add</b> is emitted. Otherwise the reference count is incremented.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (any source)</term><description>Reference count decremented. If count reaches zero, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item reference count decremented, new item reference count incremented. Add/Remove emitted as needed.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is in the result set.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// <item><term>OnError</term><description>Forwarded from any source.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when all sources have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Or, others);
    }

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Applies page-based windowing to the source list. Only items within the current page (determined by page number and page size from <paramref name="requests"/>) are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="requests">An observable of <see cref="IPageRequest"/> controlling which page to display (page number and page size).</param>
    /// <returns>An <see cref="IPageChangeSet{T}"/> stream containing only items within the current page window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains the full source list internally and calculates the page window on each change or page request.
    /// Items entering the page window produce <b>Add</b>; items leaving produce <b>Remove</b>. A new page request triggers
    /// a full recalculation of the page contents.
    /// </para>
    /// <para><b>Worth noting:</b> The source should ideally be sorted before paging, as list order determines page contents. Duplicate items are removed from the result via <c>Distinct()</c>.</para>
    /// </remarks>
    /// <seealso cref="Virtualise{T}(IObservable{IChangeSet{T}}, IObservable{IVirtualRequest})"/>
    /// <seealso cref="Top{T}(IObservable{IChangeSet{T}}, int)"/>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IPageChangeSet<T>> Page<T>(this IObservable<IChangeSet<T>> source, IObservable<IPageRequest> requests)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        requests.ThrowArgumentNullExceptionIfNull(nameof(requests));

        return new Pager<T>(source, requests).Run();
    }

    /// <summary>
    /// Subscribes to the source changeset stream and pipes all changes into the <paramref name="destination"/> <see cref="ISourceList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="destination">The destination <see cref="ISourceList{T}"/> to receive all changes.</param>
    /// <returns>An <see cref="IDisposable"/> representing the subscription. Dispose to stop piping changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Each changeset is applied to the destination using <c>Clone()</c> inside an <c>Edit()</c> call, producing a single batch update per changeset.</para>
    /// </remarks>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    public static IDisposable PopulateInto<T>(this IObservable<IChangeSet<T>> source, ISourceList<T> destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <summary>
    /// Emits a projected value from the current list snapshot after every changeset.
    /// The <paramref name="resultSelector"/> receives an <see cref="IReadOnlyCollection{T}"/> representing the current state.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the projected result.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> function projecting the current list snapshot to a result value.</param>
    /// <returns>An observable emitting the projected value after each changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Delegates to <see cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/> and applies <paramref name="resultSelector"/> via <c>Select</c>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.QueryWhenChanged"/>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// Emits an <see cref="IReadOnlyCollection{T}"/> snapshot of the current list state after every changeset.
    /// Maintains an internal list updated by cloning each changeset.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>An observable emitting the full list snapshot as <see cref="IReadOnlyCollection{T}"/> after each change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a non-changeset operator. It emits the entire collection state on each change, not incremental diffs.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange/Replace/Remove/RemoveRange/Moved/Refresh/Clear</term><description>The internal list is updated, then the full <see cref="IReadOnlyCollection{T}"/> snapshot is emitted.</description></item>
    /// <item><term>OnError</term><description>Forwarded.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> A new snapshot is emitted on every changeset, which can be chatty. The collection is rebuilt by cloning each changeset into an internal list. For sorted output, use <see cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{IReadOnlyCollection{TObject}, TDestination})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new QueryWhenChanged<T>(source).Run();
    }

    /// <summary>
    /// Reference-counted materialization of the source changeset stream into an <see cref="IObservableList{T}"/>.
    /// The shared list is created on the first subscriber and disposed when the last subscriber unsubscribes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream backed by a shared, reference-counted <see cref="IObservableList{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Equivalent to <c>Publish().RefCount()</c> for changeset streams. The underlying list is created lazily on first subscription.</para>
    /// </remarks>
    /// <seealso cref="AsObservableList{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> RefCount<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<T>(source).Run();
    }

    /// <summary>
    /// Strips index information from all changes in the stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream with all index values removed from changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Removes index positions from every change in each changeset. This is useful when downstream operators do not require or support index-based operations.</para>
    /// </remarks>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> RemoveIndex<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
    }

    /// <summary>
    /// Reverses the order of items in the changeset stream by transforming all indices: <c>new_index = length - old_index - 1</c>.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream with all index positions reversed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a pure index transformation. The items themselves are unchanged; only their positional indices are inverted.</para>
    /// </remarks>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IChangeSet<T>> Reverse<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        var reverser = new Reverser<T>();
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(reverser.Reverse(changes)));
    }

    /// <summary>
    /// Skips the initial changeset (the snapshot emitted on subscription) and forwards all subsequent changesets.
    /// Internally defers until loaded, then skips the first emission.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream that omits the initial snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Sorts the list using the specified comparer, maintaining a sorted output that incrementally updates as items change.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="comparer">The <see cref="IComparer{T}"/> comparer used for sorting.</param>
    /// <param name="options">The <see cref="SortOptions.UseBinarySearch"/> for improved performance when sorted values are immutable.</param>
    /// <param name="resort">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> that forces a full re-sort when it fires. Required when sorted property values are mutable.</param>
    /// <param name="comparerChanged">An optional <see cref="IObservable{IComparer{T}}"/> of <see cref="IComparer{T}"/> that replaces the comparer, triggering a full re-sort.</param>
    /// <param name="resetThreshold">When the number of changes exceeds this threshold, a full reset is performed instead of incremental updates. Default is 50.</param>
    /// <returns>A list changeset stream with items in sorted order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains an internal sorted list. Each incoming change is applied incrementally: adds are inserted at the correct sorted position,
    /// removes are removed by index, and refreshes re-evaluate position (emitting <b>Moved</b> if changed).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Inserted at the correct sorted position. May trigger a full reset if the count exceeds <paramref name="resetThreshold"/>.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item removed, new item inserted at sorted position.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Removed from sorted list.</description></item>
    /// <item><term><b>Refresh</b></term><description>Sort position re-evaluated. If position changed, a <b>Moved</b> is emitted.</description></item>
    /// <item><term>Comparer changed</term><description>Full re-sort of all items.</description></item>
    /// <item><term>Resort signal</term><description>Full re-sort using the current comparer.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <see cref="SortOptions.UseBinarySearch"/> is faster but requires that the values being sorted on never mutate. If they do, use the <paramref name="resort"/> signal or <see cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>.</para>
    /// </remarks>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IObservable{IComparer{T}}, SortOptions, IObservable{Unit}?, int)"/>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="ObservableCacheEx.Sort"/>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IComparer<T> comparer, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, IObservable<IComparer<T>>? comparerChanged = null, int resetThreshold = 50)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new Sort<T>(source, comparer, options, resort, comparerChanged, resetThreshold).Run();
    }

    /// <inheritdoc cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    /// <summary>
    /// Sorts the list using an observable comparer. The initial comparer is taken from the first emission; subsequent emissions trigger a full re-sort.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="comparerChanged">An <see cref="IObservable{IComparer{T}}"/> of <see cref="IComparer{T}"/> that emits comparers. The first emission provides the initial sort order; subsequent emissions trigger re-sorts.</param>
    /// <param name="options"><see cref="SortOptions"/> for controlling sort behavior.</param>
    /// <param name="resort">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> to force a re-sort with the current comparer.</param>
    /// <param name="resetThreshold">The threshold for triggering a full reset instead of incremental updates.</param>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IObservable<IComparer<T>> comparerChanged, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, int resetThreshold = 50)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparerChanged.ThrowArgumentNullExceptionIfNull(nameof(comparerChanged));

        return new Sort<T>(source, null, options, resort, comparerChanged, resetThreshold).Run();
    }

    /// <summary>
    /// Prepends an empty changeset to the source stream. Useful for initializing downstream consumers that expect an initial emission.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream that begins with an empty changeset.</returns>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.StartWith(ChangeSet<T>.Empty);

    /// <summary>
    /// Creates an <see cref="IDisposable"/> subscription for each item via <paramref name="subscriptionFactory"/> when it is added.
    /// The subscription is disposed when the item is removed or replaced. All subscriptions are disposed when the stream terminates.
    /// The changeset is forwarded downstream unmodified.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="subscriptionFactory">A function that creates an <see cref="IDisposable"/> for each item.</param>
    /// <returns>A continuation of the source changeset stream with per-item subscriptions managed as a side effect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="subscriptionFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscription created for each item via the factory. Changeset forwarded.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item's subscription disposed, new subscription created. Changeset forwarded.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscriptions for removed items are disposed. Changeset forwarded.</description></item>
    /// <item><term><b>Moved</b>/<b>Refresh</b></term><description>Forwarded. No subscription changes.</description></item>
    /// <item><term>OnError/OnCompleted/Disposal</term><description>All active subscriptions are disposed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>
    /// <seealso cref="DisposeMany{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="ObservableCacheEx.SubscribeMany"/>
    public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<T>(source, subscriptionFactory).Run();
    }

    /// <summary>
    /// Suppresses all <see cref="ListChangeReason.Refresh"/> changes from the stream. All other change reasons pass through.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>A list changeset stream with Refresh changes removed.</returns>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> SuppressRefresh<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.WhereReasonsAreNot(ListChangeReason.Refresh);

    /// <summary>
    /// Subscribes to the latest inner <see cref="IObservableList{T}"/>, switching to each new source and clearing the result when switching.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An observable that emits <see cref="IObservableList{T}"/> instances. Each emission triggers a switch to the new list.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Convenience overload that calls <c>Connect()</c> on each inner list, then delegates to <see cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>.</para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, switching to each new source and clearing the destination when switching.
    /// Previous subscriptions are disposed and the result set is emptied before subscribing to the new inner stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An <see cref="IObservable{T}"/> of <see cref="IObservable{T}"/> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// On each new inner stream, the operator clears the destination, disposes the previous subscription, and subscribes to the new stream.
    /// This is the changeset-aware equivalent of Rx's <c>Switch()</c>.
    /// </para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservableList{T}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Switch<T>(sources).Run();
    }

    /// <summary>
    /// Emits the full collection as an <see cref="IReadOnlyCollection{T}"/> after every changeset. Equivalent to <c>QueryWhenChanged(items => items)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <returns>An observable emitting the full collection snapshot after each change.</returns>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.QueryWhenChanged(items => items);

    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into the DynamicData world by converting each emitted item into a list changeset.
    /// Each emission becomes an <b>Add</b> operation in the resulting changeset stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for time-based operations (expiry, size limiting).</param>
    /// <returns>A list changeset stream where each source emission is an <b>Add</b>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary bridge from standard Rx into DynamicData's list changeset model. Each item emitted by <paramref name="source"/>
    /// is added to an internal list and an <b>Add</b> changeset is emitted. The list grows unboundedly unless size or time limits
    /// are specified via other overloads.
    /// </para>
    /// <para><b>Worth noting:</b> Source completion and errors are propagated. The internal list is disposed on unsubscribe.</para>
    /// </remarks>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{T}, Func{T, TimeSpan?}, int, IScheduler?)"/>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{IEnumerable{T}}, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with per-item time-based expiry.
    /// Expired items are automatically removed.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/>.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                Func<T, TimeSpan?> expireAfter,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with FIFO size limiting.
    /// When the list exceeds <paramref name="limitSizeTo"/>, the oldest items are removed.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/>.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/>.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                Func<T, TimeSpan?>? expireAfter,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream.
    /// Each emitted batch becomes an <b>AddRange</b>.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for time-based operations.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/>.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items are removed when the limit is exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with time-based expiry.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/>.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                Func<T, TimeSpan?> expireAfter,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/>.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items removed when exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                Func<T, TimeSpan?>? expireAfter,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <summary>
    /// Takes the first <paramref name="numberOfItems"/> items from the source list. Implemented as <c>Virtualise</c> with a fixed window starting at index 0.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="numberOfItems">The maximum number of items to include. Must be greater than zero.</param>
    /// <returns>A virtual changeset stream containing at most <paramref name="numberOfItems"/> items from the beginning of the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfItems"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>The source should ideally be sorted before applying Top, since list order determines which items appear.</para>
    /// </remarks>
    /// <seealso cref="Virtualise{T}(IObservable{IChangeSet{T}}, IObservable{IVirtualRequest})"/>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IChangeSet<T>> Top<T>(this IObservable<IChangeSet<T>> source, int numberOfItems)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }

    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted by the value returned by <paramref name="sort"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TSortKey">The type of the sort key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="sort">A <see cref="Func{T, TResult}"/> function extracting the sort key from each item.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort direction. Defaults to ascending.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject}(IObservable{IChangeSet{TObject}}, IComparer{TObject})"/>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));

    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted using the specified <paramref name="comparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> comparer used for sorting.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source, IComparer<TObject> comparer)
        where TObject : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });

    /// <summary>
    /// Projects each item to a new form using a synchronous transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="transformFactory">The <see cref="Func{T, TResult}"/> transform function applied to each item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory and emit an update. When <see langword="false"/> (the default), Refresh is forwarded without re-transforming.</param>
    /// <returns>A list changeset stream of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Maintains an internal list of transformed items. Each source changeset is
    /// processed and a corresponding output changeset is produced with the transformed items.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The factory is called and an <b>Add</b> is emitted at the same index.</description></item>
    /// <item><term>AddRange</term><description>The factory is called for each item. An <b>AddRange</b> is emitted at the same start index.</description></item>
    /// <item><term>Replace</term><description>The factory is called for the new item. A <b>Replace</b> is emitted at the same index. The previous transformed value is available to overloads that accept <see cref="Optional{TDestination}"/>.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted (no factory call).</description></item>
    /// <item><term>RemoveRange</term><description>A <b>RemoveRange</b> is emitted.</description></item>
    /// <item><term>Moved</term><description>A <b>Moved</b> is emitted with updated indices (no factory call). Throws <see cref="UnspecifiedIndexException"/> if the source change has no index information.</description></item>
    /// <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is <see langword="false"/> (default), the <b>Refresh</b> is forwarded without re-transforming. If <see langword="true"/>, the factory is re-invoked and the result replaces the current value.</description></item>
    /// <item><term>Clear</term><description>A <b>Clear</b> is emitted and the internal list is emptied.</description></item>
    /// <item><term>OnError</term><description>Forwarded. If the factory throws, the exception propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> By default, Refresh does NOT re-transform the item (it just forwards the signal). Set <paramref name="transformOnRefresh"/> to <see langword="true"/> if you need the factory re-invoked on Refresh. Add operations with out-of-bounds indices silently append to the end.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <seealso cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="Convert{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination})"/>
    /// <seealso cref="ObservableCacheEx.Transform"/>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that also receives the item's index.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="transformFactory">A <see cref="Func{T, TResult}"/> function receiving the source item and its index, returning the transformed item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, _, idx) => transformFactory(t, idx), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that also receives the previously transformed value (if any).
    /// Type arguments must be specified explicitly as type inference fails for this overload.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="transformFactory">A function receiving the source item and the previous transformed value (as <see cref="Optional{T}"/>), returning the new transformed item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, previous, _) => transformFactory(t, previous), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that receives the source item, the previously transformed value, and the index.
    /// Type arguments must be specified explicitly as type inference fails for this overload.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="transformFactory">A <see cref="Func{T, TResult}"/> function receiving the source item, previous transformed value, and index.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new Transformer<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }

    /// <summary>
    /// Projects each item to a new form using an async transform function. Behaves like <see cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/> but the factory returns a <see cref="Task{T}"/>.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="transformFactory">An <see cref="Func{T, TResult}"/> async function that transforms each source item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    /// <returns>A list changeset stream of asynchronously transformed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Change handling is identical to the synchronous <see cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/> except the factory is awaited. Operations are serialized per changeset via a semaphore.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>The async factory is awaited for each item. An <b>Add</b>/<b>AddRange</b> is emitted with the transformed results.</description></item>
    /// <item><term>Replace</term><description>The async factory is awaited for the new item. A <b>Replace</b> is emitted.</description></item>
    /// <item><term>Remove/RemoveRange</term><description>Emitted without invoking the factory.</description></item>
    /// <item><term>Moved</term><description>Emitted with updated indices (no factory call).</description></item>
    /// <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is <see langword="false"/> (default), forwarded without re-transforming. If <see langword="true"/>, the factory is re-awaited.</description></item>
    /// <item><term>Clear</term><description>Emitted and internal list cleared.</description></item>
    /// <item><term>OnError</term><description>Forwarded. If the async factory throws, the exception propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded after the last changeset is processed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> All async transforms within a single changeset are serialized (not parallel). Each changeset is fully processed before the next begins. By default, Refresh does NOT re-transform.</para>
    /// </remarks>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <seealso cref="ObservableCacheEx.TransformAsync"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item and its index.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, i) => transformFactory(t, i), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item and the previously transformed value.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, d, _) => transformFactory(t, d), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item, previously transformed value, and index. This is the terminal overload that all other TransformAsync overloads delegate to.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }

    /// <summary>
    /// Flattens each source item into multiple destination items using <paramref name="manySelector"/>. Each source item produces zero or more children,
    /// all of which are merged into a single flat list changeset stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="manySelector">A <see cref="Func{T, TResult}"/> function that returns the child items for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> comparer used during Replace to determine which child items changed between old and new parent values.</param>
    /// <returns>A list changeset stream of all child items from all source items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Children expanded and added to the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old children diffed against new children (using <paramref name="equalityComparer"/>). Removed, added, or kept as appropriate.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>All children of the removed parents are removed from the output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Children re-expanded and diffed.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="ObservableCacheEx.TransformMany"/>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
    }

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="ObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from a <see cref="ReadOnlyObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="IObservableList{T}"/>. The inner list is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <summary>
    /// Applies a sliding window to the source list using start index and size from <paramref name="requests"/>.
    /// Only items within the window are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="requests">An observable of <see cref="IVirtualRequest"/> specifying the start index and size of the window.</param>
    /// <returns>An <see cref="IVirtualChangeSet{T}"/> stream containing only items within the current virtual window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Like <see cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/> but uses absolute start index and size instead of page number and page size.
    /// Internally maintains the full source list and recalculates the window on each change or request.
    /// </para>
    /// </remarks>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Top{T}(IObservable{IChangeSet{T}}, int)"/>
    public static IObservable<IVirtualChangeSet<T>> Virtualise<T>(this IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        requests.ThrowArgumentNullExceptionIfNull(nameof(requests));

        return new Virtualiser<T>(source, requests).Run();
    }

    /// <summary>
    /// Watches all items in the source list and emits the item when any of its properties change.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertiesToMonitor">An optional list of property names to monitor. If empty, all property changes are observed.</param>
    /// <returns>An observable emitting the item whenever any monitored property changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Implemented via <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>. Subscriptions are managed per item: created on add, disposed on remove.</para>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenValueChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.WhenAnyPropertyChanged"/>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject>(this IObservable<IChangeSet<TObject>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }

    /// <summary>
    /// Watches a specific property on all items in the source list and emits a <see cref="PropertyValue{TObject, TValue}"/> (item + value pair) when it changes.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of item. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertyAccessor">An <see cref="Expression{TDelegate}"/> expression selecting the property to observe.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (default), the current value is emitted immediately upon subscribing to each item.</param>
    /// <returns>An observable emitting <see cref="PropertyValue{TObject, TValue}"/> whenever the property changes on any tracked item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyAccessor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Implemented via <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>.</para>
    /// </remarks>
    /// <seealso cref="WhenValueChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject}(IObservable{IChangeSet{TObject}}, string[])"/>
    /// <seealso cref="ObservableCacheEx.WhenPropertyChanged"/>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue));
    }

    /// <summary>
    /// Watches a specific property on all items and emits just the property value (without the sender) when it changes.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of item. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="propertyAccessor">An <see cref="Expression{TDelegate}"/> expression selecting the property to observe.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (default), the current value is emitted immediately upon subscribing to each item.</param>
    /// <returns>An observable emitting the property value whenever it changes on any tracked item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyAccessor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject}(IObservable{IChangeSet{TObject}}, string[])"/>
    /// <seealso cref="ObservableCacheEx.WhenValueChanged"/>
    public static IObservable<TValue?> WhenValueChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue).Select(pv => pv.Value));
    }

    /// <summary>
    /// Filters the changeset stream to include only changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to include. Must specify at least one.</param>
    /// <returns>A list changeset stream containing only changes with the specified reasons.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>Filters individual changes within each changeset. If filtering removes all changes from a changeset, the empty changeset is suppressed via <see cref="NotEmpty{T}(IObservable{IChangeSet{T}})"/>.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Any matching reason</term><description>The change is included in the output. Index information is stripped.</description></item>
    /// <item><term>Any non-matching reason</term><description>The change is dropped from the output.</description></item>
    /// <item><term>OnError</term><description>Forwarded.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes can cause downstream operators to accumulate items indefinitely (memory leak). Index information is stripped because removing some changes invalidates the original index positions.</para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            changes =>
            {
                var filtered = changes.Where(change => matches.Contains(change.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }

    /// <summary>
    /// Filters the changeset stream to exclude changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// The exception is when only <see cref="ListChangeReason.Refresh"/> is excluded, since removing Refresh does not affect index calculations.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to exclude. Must specify at least one.</param>
    /// <returns>A list changeset stream with the specified change reasons removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// Empty changesets (after filtering) are automatically suppressed. When only <see cref="ListChangeReason.Refresh"/> is excluded,
    /// indices are preserved, since removing Refresh does not affect index calculations.
    /// </para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAre{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        if (reasons.Length == 1 && reasons[0] == ListChangeReason.Refresh)
        {
            // If only refresh changes are removed, then there's no need to remove the indexes
            return source.Select(changes =>
            {
                var filtered = changes.Where(c => c.Reason != ListChangeReason.Refresh);
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            updates =>
            {
                var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }

    /// <summary>
    /// Applies a logical XOR (symmetric difference) between the source and other streams.
    /// Items present in exactly one source are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> of <see cref="IChangeSet{T}"/>.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to combine with.</param>
    /// <returns>A list changeset stream containing items that exist in exactly one source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Uses reference-counted equality. An item is included when it exists in exactly one source.
    /// If it appears in a second source, it is removed from the result. If it then leaves one source,
    /// it re-enters the result. <b>Moved</b> changes are ignored.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Reference count updated. If the item is now in exactly one source, an <b>Add</b> is emitted. If now in two or more, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Reference count decremented. If now in exactly one source, an <b>Add</b> is emitted. If now in zero, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item reference count decremented, new item incremented, with Xor logic applied.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if item is in the result set.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// <item><term>OnError</term><description>Forwarded from any source.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded when all sources have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.Xor"/>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Applies a logical XOR between a pre-built collection of list changeset sources.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    private static IObservable<IChangeSet<T>> Combine<T>(this ICollection<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Combiner<T>(sources, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservable<IChangeSet<T>> source, CombineOperator type, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = source.EnumerateOne().Union(others).ToList();
        return new Combiner<T>(items, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<ISourceList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservableList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new DynamicCombiner<T>(sources, type).Run();
    }
}
