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
public static partial class ObservableListEx
{
    /// <summary>
    /// Monitors each item with a custom observable and emits <b>Refresh</b> changes whenever that observable fires,
    /// causing downstream operators (Filter, Sort, Group) to re-evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TAny">The type emitted by the re-evaluator observable (value is ignored).</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">A <see cref="Func{T, TResult}"/> factory that, given an item, returns an observable whose emissions trigger a <b>Refresh</b> for that item.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration to batch refresh signals into a single changeset.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for buffering.</param>
    /// <returns>A list changeset stream with additional <b>Refresh</b> changes injected when per-item observables fire.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reevaluator"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the general-purpose refresh mechanism. <see cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// is a convenience wrapper that uses <c>WhenAnyPropertyChanged()</c> as the re-evaluator.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>Subscribes to the re-evaluator observable for each new item. The original change is forwarded.</description></item>
    /// <item><term>Replace</term><description>Unsubscribes from the old item's observable, subscribes to the new. The original change is forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>Unsubscribes from removed items. The original change is forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>Forwarded unchanged.</description></item>
    /// <item><term>Re-evaluator fires</term><description>The item's current index is looked up and a <b>Refresh</b> change is emitted.</description></item>
    /// <item><term>OnError</term><description>Errors from any per-item re-evaluator terminate the output. Source errors also terminate the output.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Prior to v9, per-item re-evaluator errors were silently ignored. As of v9 they propagate via the underlying <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.AutoRefreshOnObservable{TObject, TKey, TAny}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TAny}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return new AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }
}
