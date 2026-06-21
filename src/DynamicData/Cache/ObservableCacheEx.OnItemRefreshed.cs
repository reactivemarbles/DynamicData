// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item refresh events in.</param>
    /// <param name="refreshAction">The <see cref="Action{TObject, TKey}"/> callback invoked for each refreshed item. Receives the item and its key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Ignored.</description></item>
    ///   <item><term>Update</term><description>Ignored.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Invokes <paramref name="refreshAction"/> with the item and key.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="refreshAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="refreshAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AutoRefresh{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableListEx.OnItemRefreshed"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> refreshAction)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(refreshAction);

        return source.OnChangeAction(ChangeReason.Refresh, refreshAction);
    }

    /// <inheritdoc cref="OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item refresh events in.</param>
    /// <param name="refreshAction">The <see cref="Action{TObject}"/> callback invoked for each refreshed item. Receives only the item (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRefreshed<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> refreshAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRefreshed((obj, _) => refreshAction(obj));
}
