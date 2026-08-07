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
    /// Filters each item using a per-item <see cref="IObservable{T}"/> of <see cref="bool"/> that dynamically controls inclusion.
    /// When an item's observable emits <see langword="true"/> the item enters the result; when it emits <see langword="false"/> the item is removed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to filter by property value.</param>
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
    /// </list>
    /// <list type="table">
    /// <listheader><term>Event (per-item observable)</term><description>Behavior</description></listheader>
    /// <item><term>Emits <see langword="true"/></term><description>If not already included, an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term>Emits <see langword="false"/></term><description>If currently included, a <b>Remove</b> is emitted downstream.</description></item>
    /// <item><term>OnError</term><description>Errors from any per-item filter observable terminate the output. Source errors also terminate the output.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<TObject>> FilterOnObservable<TObject>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> objectFilterObservable, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new FilterOnObservable<TObject>(source, objectFilterObservable, propertyChangedThrottle, scheduler).Run();
    }
}
