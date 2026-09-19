// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Invokes <paramref name="removeAction"/> for every item removed from the source list stream.
    /// Triggers on <see cref="ListChangeReason.Remove"/>, <see cref="ListChangeReason.RemoveRange"/>, <see cref="ListChangeReason.Clear"/>, and the old item of <see cref="ListChangeReason.Replace"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to observe item removals in.</param>
    /// <param name="removeAction">The <c>Action&lt;T&gt;</c> action to invoke for each removed item.</param>
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
    /// <seealso><c>OnItemAdded&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Action&lt;T&gt;)</c></seealso>
    /// <seealso><c>DisposeMany&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>SubscribeMany&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, IDisposable&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.OnItemRemoved&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Action&lt;TObject&gt;, bool)</c></seealso>
    public static IObservable<IChangeSet<T>> OnItemRemoved<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> removeAction,
                bool invokeOnUnsubscribe = true)
            where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        return List.Internal.OnItemRemoved<T>.Create(
                source: source,
                removeAction: removeAction,
                invokeOnUnsubscribe: invokeOnUnsubscribe);
    }
}
