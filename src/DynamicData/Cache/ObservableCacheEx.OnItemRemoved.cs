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
    /// Invokes <paramref name="removeAction"/> for each item with <see cref="ChangeReason.Remove"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item removals in.</param>
    /// <param name="removeAction">The <c>Action&lt;TObject, TKey&gt;</c> callback invoked for each removed item. Receives the removed item and its key.</param>
    /// <param name="invokeOnUnsubscribe">
    /// When <see langword="true"/> (the default), the callback is also invoked for <b>every item still in the cache</b>
    /// when the subscription is disposed. When <see langword="false"/>, only inline Remove changes trigger the callback.
    /// </param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored (but tracked internally when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>).</description></item>
    ///   <item><term>Update</term><description>Ignored (cache updated internally when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>).</description></item>
    ///   <item><term>Remove</term><description>Invokes <paramref name="removeAction"/> with the item and key.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Unsubscribe behavior:</b> when <paramref name="invokeOnUnsubscribe"/> is <see langword="true"/>, the operator
    /// maintains an internal cache mirroring the stream. On disposal, it iterates all remaining items and
    /// invokes <paramref name="removeAction"/> for each. This is useful for cleanup logic (e.g. event unsubscription)
    /// that must run for items that were never explicitly removed.
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="removeAction"/> propagate as <c>OnError</c> during inline removes.
    /// During unsubscribe disposal, exceptions are not caught.
    /// </para>
    /// <para><b>Worth noting:</b> The action also fires for ALL remaining items when the subscription is disposed (unless <c>invokeOnUnsubscribe</c> is <see langword="false"/>). The action runs under a lock; avoid calling into other caches from within it.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="removeAction"/> is <see langword="null"/>.</exception>
    /// <seealso><c>DisposeMany&lt;TObject,TKey&gt;</c></seealso>
    /// <seealso><c>SubscribeMany&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IDisposable&gt;)</c></seealso>
    /// <seealso><c>ObservableListEx.OnItemRemoved</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(removeAction);

        if (invokeOnUnsubscribe)
        {
            return new OnBeingRemoved<TObject, TKey>(source, removeAction).Run();
        }

        return source.OnChangeAction(ChangeReason.Remove, removeAction);
    }

    /// <summary>
    /// Provides an overload of <c>removeAction</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item removals in.</param>
    /// <param name="removeAction">The <c>Action&lt;TObject&gt;</c> callback invoked for each removed item. Receives only the item (no key).</param>
    /// <param name="invokeOnUnsubscribe">When <see langword="true"/> (the default), also invoked for all remaining items on disposal.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that omits the key from the callback. Delegates to <c>OnItemRemoved&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Action&lt;TObject, TKey&gt;, bool)</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRemoved((obj, _) => removeAction(obj), invokeOnUnsubscribe);
}
