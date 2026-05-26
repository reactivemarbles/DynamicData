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
/// ObservableList extensions for subscription lifecycle, disposal, and population.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Disposes items that implement <see cref="IDisposable"/> when they are removed, replaced, or cleared from the stream.
    /// All remaining tracked items are disposed when the stream finalizes (OnCompleted, OnError, or subscription disposal).
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to track for disposal on removal.</param>
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
    /// <seealso cref="ObservableCacheEx.DisposeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<T>(source).Run();
    }

    /// <summary>
    /// Subscribes to the source changeset stream and pipes all changes into the <paramref name="destination"/> <see cref="ISourceList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to pipe into a target list.</param>
    /// <param name="destination">The destination <see cref="ISourceList{T}"/> to receive all changes.</param>
    /// <returns>An <see cref="IDisposable"/> representing the subscription. Dispose to stop piping changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Each changeset is applied to the destination using <c>Clone()</c> inside an <c>Edit()</c> call, producing a single batch update per changeset.</para>
    /// </remarks>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="ObservableCacheEx.PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    public static IDisposable PopulateInto<T>(this IObservable<IChangeSet<T>> source, ISourceList<T> destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

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
