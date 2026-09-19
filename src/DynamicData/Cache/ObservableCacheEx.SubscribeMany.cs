// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to create a subscription for each item in.</param>
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
    /// Internally implemented using <c>Transform&lt;TDestination,TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Func&lt;TObject,TKey,TDestination&gt;, bool)</c>
    /// and <c>DisposeMany&lt;TObject,TKey&gt;</c>, so disposal semantics match <c>DisposeMany&lt;TObject,TKey&gt;</c>.
    /// </para>
    /// <para>
    /// Use this to tie per-item side effects (event subscriptions, polling timers, child observable subscriptions)
    /// to the lifecycle of items in the cache.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="subscriptionFactory"/> is <see langword="null"/>.</exception>
    /// <seealso><c>DisposeMany&lt;TObject,TKey&gt;</c></seealso>
    /// <seealso><c>OnItemRemoved&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableListEx.SubscribeMany</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }

    /// <summary>
    /// Provides an overload of <c>SubscribeMany</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to create a subscription for each item in.</param>
    /// <param name="subscriptionFactory">A factory that creates an <see cref="IDisposable"/> for each item. Receives the item and its key.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload whose factory receives both the item and the key. See <c>SubscribeMany&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, IDisposable&gt;)</c> for full details.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

        return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }
}
