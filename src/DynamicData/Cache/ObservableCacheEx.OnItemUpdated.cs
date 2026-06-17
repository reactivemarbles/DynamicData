// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

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
}
