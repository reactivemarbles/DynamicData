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
/// ObservableList extensions for per-item change-reason notifications.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Invokes <paramref name="addAction"/> for every item added to the source list stream.
    /// Triggers on <see cref="ListChangeReason.Add"/>, <see cref="ListChangeReason.AddRange"/>, and the new item of <see cref="ListChangeReason.Replace"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe item additions in.</param>
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
    /// <item><term>OnError</term><description>If the callback throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="OnItemRefreshed{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject})"/>
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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe item refresh events in.</param>
    /// <param name="refreshAction">The <see cref="Action{T}"/> action to invoke for each refreshed item.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="refreshAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject})"/>
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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe item removals in.</param>
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
    /// <seealso cref="ObservableCacheEx.OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject}, bool)"/>
    public static IObservable<IChangeSet<T>> OnItemRemoved<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> removeAction,
                bool invokeOnUnsubscribe = true)
            where T : notnull
        => List.Internal.OnItemRemoved<T>.Create(
            source: source,
            removeAction: removeAction,
            invokeOnUnsubscribe: invokeOnUnsubscribe);
}
