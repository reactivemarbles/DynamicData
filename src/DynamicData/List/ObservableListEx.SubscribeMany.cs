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
    /// Creates an <see cref="IDisposable"/> subscription for each item via <paramref name="subscriptionFactory"/> when it is added.
    /// The subscription is disposed when the item is removed or replaced. All subscriptions are disposed when the stream terminates.
    /// The changeset is forwarded downstream unmodified.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to create a subscription for each item in.</param>
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
    /// <seealso cref="ObservableCacheEx.SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/>
    public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<T>(source, subscriptionFactory).Run();
    }
}
