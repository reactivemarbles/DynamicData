// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
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
    /// Monitors each item with a custom observable and emits <b>Refresh</b> changes whenever that observable fires,
    /// causing downstream operators (Filter, Sort, Group) to re-evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TAny">The type emitted by the re-evaluator observable (value is ignored).</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">A <c>Func&lt;T, TResult&gt;</c> factory that, given an item, returns an observable whose emissions trigger a <b>Refresh</b> for that item.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch refresh signals into a single changeset.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for buffering.</param>
    /// <returns>A list changeset stream with additional <b>Refresh</b> changes injected when per-item observables fire.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reevaluator"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the general-purpose refresh mechanism. <c>AutoRefresh&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c>
    /// is a convenience wrapper that uses <c>WhenAnyPropertyChanged()</c> as the re-evaluator.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Subscribes to the re-evaluator observable for each new item. The original change is forwarded.</description></item>
    /// <item><term>Replace</term><description>Unsubscribes from the old item's observable, subscribes to the new. The original change is forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>Unsubscribes from removed items. The original change is forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>Forwarded unchanged.</description></item>
    /// <item><term>Re-evaluator fires</term><description>The item's current index is looked up and a <b>Refresh</b> change is emitted.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>AutoRefresh&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>SuppressRefresh&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.AutoRefreshOnObservable&lt;TObject, TKey, TAny&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, IObservable&lt;TAny&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(reevaluator);

        return new AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }
}
