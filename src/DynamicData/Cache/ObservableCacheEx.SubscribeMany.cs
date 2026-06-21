// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }
}
