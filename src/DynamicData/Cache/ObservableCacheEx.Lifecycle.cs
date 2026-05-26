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
/// ObservableCache extensions for subscription lifecycle and disposal.
/// </summary>
public static partial class ObservableCacheEx
{
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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to track for async disposal on removal.</param>
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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to track for disposal on removal.</param>
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
    /// <seealso cref="ObservableListEx.DisposeMany"/>
    public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// Obsolete: do not use. This can cause unhandled exception issues. Use the standard Rx <c>Finally</c> operator instead.
    /// </summary>
    /// <typeparam name="T">The type contained within the observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to attach a finally action to.</param>
    /// <param name="finallyAction">The <see cref="Action"/> to invoke when the subscription terminates.</param>
    /// <returns>An observable which has always a finally action applied.</returns>
    [Obsolete("This can cause unhandled exception issues so do not use")]
    public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        finallyAction.ThrowArgumentNullExceptionIfNull(nameof(finallyAction));

        return new FinallySafe<T>(source, finallyAction).Run();
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every individual <see cref="Change{TObject,TKey}"/> in each changeset,
    /// regardless of change reason. The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe each individual change in.</param>
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
    /// <seealso cref="ObservableListEx.ForEachChange"/>
    public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<Change<TObject, TKey>> action)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.ForEach(action));
    }

    /// <summary>
    /// Monitors the source observable and emits <see cref="ConnectionStatus"/> values: <c>Pending</c> initially,
    /// <c>Loaded</c> when the first value arrives, <c>Errored</c> on error, and <c>Completed</c> on completion.
    /// This is not a changeset operator.
    /// </summary>
    /// <typeparam name="T">The type of the source observable.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to monitor for connection status.</param>
    /// <returns>An observable that emits <see cref="ConnectionStatus"/> values reflecting the source's lifecycle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source) => new StatusMonitor<T>(source).Run();

    /// <summary>
    /// Creates an <see cref="IDisposable"/> subscription per item via <paramref name="subscriptionFactory"/>.
    /// Subscriptions are created on Add/Update and disposed on Update/Remove. All active subscriptions
    /// are disposed when the stream completes, errors, or the subscription is disposed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to create a subscription for each item in.</param>
    /// <param name="subscriptionFactory">A factory that creates an <see cref="IDisposable"/> for each item. Called on Add and Update (for the new value).</param>
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
    /// <seealso cref="ObservableListEx.SubscribeMany"/>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }

    /// <inheritdoc cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to create a subscription for each item in.</param>
    /// <param name="subscriptionFactory">A factory that creates an <see cref="IDisposable"/> for each item. Receives the item and its key.</param>
    /// <remarks>Overload whose factory receives both the item and the key. See <see cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/> for full details.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }
}
