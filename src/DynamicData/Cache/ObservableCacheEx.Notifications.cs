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
/// ObservableCache extensions for per-item change-reason notifications.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item additions in.</param>
    /// <param name="addAction">The <see cref="Action{TObject, TKey}"/> callback invoked for each added item. Receives the new item and its key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Invokes <paramref name="addAction"/> with the item and key.</description></item>
    ///   <item><term>Update</term><description>Ignored.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="addAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="addAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemUpdated{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TObject,TKey})"/>
    /// <seealso cref="OnItemRemoved{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey}, bool)"/>
    /// <seealso cref="ForEachChange{TObject,TKey}"/>
    /// <seealso cref="ObservableListEx.OnItemAdded"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> addAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));

        return source.OnChangeAction(ChangeReason.Add, addAction);
    }

    /// <inheritdoc cref="OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item additions in.</param>
    /// <param name="addAction">The <see cref="Action{TObject}"/> callback invoked for each added item. Receives only the item (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> addAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemAdded((obj, _) => addAction(obj));

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        refreshAction.ThrowArgumentNullExceptionIfNull(nameof(refreshAction));

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

    /// <summary>
    /// Invokes <paramref name="removeAction"/> for each item with <see cref="ChangeReason.Remove"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item removals in.</param>
    /// <param name="removeAction">The <see cref="Action{TObject, TKey}"/> callback invoked for each removed item. Receives the removed item and its key.</param>
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
    /// <seealso cref="DisposeMany{TObject,TKey}"/>
    /// <seealso cref="SubscribeMany{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IDisposable})"/>
    /// <seealso cref="ObservableListEx.OnItemRemoved"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));

        if (invokeOnUnsubscribe)
        {
            return new OnBeingRemoved<TObject, TKey>(source, removeAction).Run();
        }

        return source.OnChangeAction(ChangeReason.Remove, removeAction);
    }

    /// <inheritdoc cref="OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey}, bool)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item removals in.</param>
    /// <param name="removeAction">The <see cref="Action{TObject}"/> callback invoked for each removed item. Receives only the item (no key).</param>
    /// <param name="invokeOnUnsubscribe">When <see langword="true"/> (the default), also invoked for all remaining items on disposal.</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemRemoved{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TKey}, bool)"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction, bool invokeOnUnsubscribe = true)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemRemoved((obj, _) => removeAction(obj), invokeOnUnsubscribe);

    /// <summary>
    /// Invokes <paramref name="updateAction"/> for each item with <see cref="ChangeReason.Update"/> in the changeset stream.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item updates in.</param>
    /// <param name="updateAction">The <see cref="Action{TObject, TObject, TKey}"/> callback invoked for each updated item. Receives the current value, previous value, and key.</param>
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
    /// <seealso cref="OnItemAdded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}}, Action{TObject,TKey})"/>
    /// <seealso cref="ForEachChange{TObject,TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject, TKey> updateAction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.OnChangeAction(static change => change.Reason == ChangeReason.Update, change => updateAction(change.Current, change.Previous.Value, change.Key));
    }

    /// <inheritdoc cref="OnItemUpdated{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe item updates in.</param>
    /// <param name="updateAction">The <see cref="Action{TObject, TObject}"/> callback invoked for each updated item. Receives only the current and previous values (no key).</param>
    /// <remarks>Overload that omits the key from the callback. Delegates to <see cref="OnItemUpdated{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject, TObject, TKey})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemUpdated((cur, prev, _) => updateAction(cur, prev));

    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Predicate<Change<TObject, TKey>> predicate, Action<Change<TObject, TKey>> changeAction)
        where TObject : notnull
        where TKey : notnull
    {
        return source.Do(changes =>
        {
            foreach (var change in changes.ToConcreteType())
            {
                if (!predicate(change))
                {
                    continue;
                }

                changeAction(change);
            }
        });
    }

    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ChangeReason reason, Action<TObject, TKey> action)
        where TObject : notnull
        where TKey : notnull
        => source.OnChangeAction(change => change.Reason == reason, change => action(change.Current, change.Key));
}
