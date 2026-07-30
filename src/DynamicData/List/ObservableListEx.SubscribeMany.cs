// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to create a subscription for each item in.</param>
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
    /// <seealso><c>MergeMany&lt;T, TDestination&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, IObservable&lt;TDestination&gt;&gt;)</c></seealso>
    /// <seealso><c>DisposeMany&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>OnItemRemoved&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Action&lt;T&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableCacheEx.SubscribeMany&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, IDisposable&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(subscriptionFactory);

        return new SubscribeMany<T>(source, subscriptionFactory).Run();
    }
}
