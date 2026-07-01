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
    /// Subscribes to a child observable for each item in the source cache changeset stream and merges all child
    /// emissions into a single <c>IObservable&lt;T&gt;</c>. When an item is added, <paramref name="observableSelector"/>
    /// creates its child subscription. When updated, the previous child subscription is disposed and a new one is created.
    /// When removed, its child subscription is disposed. Refresh changes have no effect on subscriptions.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <c>Func&lt;T, TResult&gt;</c> factory function that produces a child observable for each source item.</param>
    /// <returns>An observable that emits values from all active child observables, interleaved by arrival order.</returns>
    /// <remarks>
    /// <para>
    /// This operator does not produce changesets. It produces a flat stream of <typeparamref name="TDestination"/>
    /// values, similar to Rx <c>SelectMany</c> but lifecycle-aware: child subscriptions track items entering and
    /// leaving the source cache.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> to create a child observable and subscribes to it. Emissions from the child flow into the merged output.</description></item>
    /// <item><term>Update</term><description>Disposes the previous child subscription and creates a new one for the updated item.</description></item>
    /// <item><term>Remove</term><description>Disposes the child subscription for the removed item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The child observable continues unchanged.</description></item>
    /// <item><term>OnError</term><description>Errors from child observables are silently swallowed (the child is unsubscribed). Errors from the source changeset stream terminate the merged output.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The output is a plain <c>IObservable&lt;TDestination&gt;</c>, not a changeset stream. If you need merged changesets, use <c>MergeManyChangeSets&lt;TObject, TKey, TDestination, TDestinationKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IObservable&lt;IChangeSet&lt;TDestination, TDestinationKey&gt;&gt;&gt;, IComparer&lt;TDestination&gt;, IEqualityComparer&lt;TDestination&gt;)</c> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    /// <seealso><c>MergeManyChangeSets&lt;TObject, TKey, TDestination, TDestinationKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IObservable&lt;IChangeSet&lt;TDestination, TDestinationKey&gt;&gt;&gt;, IComparer&lt;TDestination&gt;, IEqualityComparer&lt;TDestination&gt;)</c></seealso>
    /// <seealso><c>MergeChangeSets&lt;TObject, TKey&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;&gt;)</c></seealso>
    /// <seealso><c>SubscribeMany&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IDisposable&gt;)</c></seealso>
    /// <seealso><c>ObservableListEx.MergeMany</c></seealso>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Provides an overload of <c>MergeMany</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <c>Func&lt;T, TResult&gt;</c> factory function that receives both the item and its key, and returns a child observable.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }
}
