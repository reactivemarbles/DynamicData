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
}
