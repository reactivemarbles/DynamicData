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
    /// Subscribes to a per-item observable for each item in the source and merges all emissions into a single <see cref="IObservable{TDestination}"/> stream.
    /// This is NOT a changeset operator: it returns a flat observable of values.
    /// </summary>
    /// <typeparam name="T">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by per-item observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns an observable for each source item.</param>
    /// <returns>An observable that emits values from all per-item observables, merged together.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Subscription behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed.</description></item>
    /// <item><term><b>Refresh</b>/<b>Moved</b></term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnCompleted (source)</term><description>Completes only after the source and all active inner observables have completed.</description></item>
    /// <item><term>OnError</term><description>Errors from any per-item observable terminate the merged output. Source errors also terminate the output.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Prior to v9, errors from per-item observables were silently swallowed and the offending item was unsubscribed while the merged stream continued. As of v9 child errors propagate, matching the semantics of standard <c>Observable.Merge</c>.</para>
    /// </remarks>
    /// <seealso cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="ObservableCacheEx.MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<T, TDestination>(source, observableSelector).Run();
    }
}
