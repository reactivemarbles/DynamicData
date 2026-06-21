// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Monitors all properties on each item (via <see cref="INotifyPropertyChanged"/>) and emits <b>Refresh</b>
    /// changes when any property changes, causing downstream operators to re-evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of items, which must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to monitor for property-driven refresh signals.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch multiple refresh signals into a single changeset.</param>
    /// <param name="propertyChangeThrottle">An optional <see cref="TimeSpan"/> throttle applied to each item's property change notifications.</param>
    /// <param name="scheduler">The scheduler for throttle and buffer timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>A list changeset stream with additional <b>Refresh</b> changes injected when properties change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Wraps <c>AutoRefreshOnObservable&lt;TObject, TAny&gt;</c> using <c>WhenAnyPropertyChanged()</c> as the re-evaluator.
    /// Pair with <c>Filter&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, bool&gt;)</c> or <c>Sort&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IComparer&lt;T&gt;, SortOptions, IObservable&lt;Unit&gt;?, IObservable&lt;IComparer&lt;T&gt;&gt;?, int)</c>
    /// to get reactive re-evaluation on property changes.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Subscribes to <c>PropertyChanged</c> on each new item. The original change is forwarded.</description></item>
    /// <item><term>Replace</term><description>Unsubscribes from the old item, subscribes to the new. The original change is forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>Unsubscribes from removed items. The original change is forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>Forwarded unchanged.</description></item>
    /// <item><term>Property changes</term><description>A <b>Refresh</b> change is emitted for the item whose property changed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Each item generates a subscription. For large lists with frequent property changes, use <paramref name="changeSetBuffer"/> and <paramref name="propertyChangeThrottle"/> to reduce churn.</para>
    /// </remarks>
    /// <seealso><c>AutoRefresh&lt;TObject, TProperty&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TProperty&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>AutoRefreshOnObservable&lt;TObject, TAny&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, IObservable&lt;TAny&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>SuppressRefresh&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.AutoRefresh&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

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

    /// <summary>
    /// Monitors a single property (selected by <paramref name="propertyAccessor"/>) on each item via <see cref="INotifyPropertyChanged"/>
    /// and emits <b>Refresh</b> changes when that property changes, causing downstream operators to re-evaluate. More efficient than
    /// the all-properties overload when only one property (of type <typeparamref name="TProperty"/>) affects downstream behavior.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TProperty">The type of the TProperty value.</typeparam>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <param name="source">The source value.</param>
    /// <param name="propertyAccessor">The propertyAccessor value.</param>
    /// <param name="changeSetBuffer">The changeSetBuffer value.</param>
    /// <param name="propertyChangeThrottle">The propertyChangeThrottle value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertyAccessor, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertyAccessor);

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
}
