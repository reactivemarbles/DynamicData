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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Invokes <paramref name="updateAction"/> for each item with <see cref="ChangeReason.Update"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item updates in.</param>
    /// <param name="updateAction">The <c>Action&lt;TObject, TObject, TKey&gt;</c> callback invoked for each updated item. Receives the current value, previous value, and key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored.</description></item>
    ///   <item><term>Update</term><description>Invokes <paramref name="updateAction"/> with (current, previous, key). The previous value is always available for Update changes.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="updateAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="updateAction"/> is <see langword="null"/>.</exception>
    /// <seealso><c>OnItemAdded&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;)</c></seealso>
    /// <seealso><c>ForEachChange&lt;TObject,TKey&gt;</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject, TKey> updateAction)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        return source.OnChangeAction(static change => change.Reason == ChangeReason.Update, change => updateAction(change.Current, change.Previous.Value, change.Key));
    }

    /// <summary>
    /// Provides an overload of <c>updateAction</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item updates in.</param>
    /// <param name="updateAction">The <c>Action&lt;TObject, TObject&gt;</c> callback invoked for each updated item. Receives only the current and previous values (no key).</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that omits the key from the callback. Delegates to <c>OnItemUpdated&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Action&lt;TObject, TObject, TKey&gt;)</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemUpdated((cur, prev, _) => updateAction(cur, prev));
}
