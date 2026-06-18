// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Invokes <paramref name="addAction"/> for every item added to the source list stream.
    /// Triggers on <see cref="ListChangeReason.Add"/>, <see cref="ListChangeReason.AddRange"/>, and the new item of <see cref="ListChangeReason.Replace"/>.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe item additions in.</param>
    /// <param name="addAction">The <see cref="Action{T}"/> action to invoke for each added item.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="addAction"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The action fires before the changeset is forwarded downstream.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Callback invoked with the added item. Changeset forwarded.</description></item>
    /// <item><term>AddRange</term><description>Callback invoked for each item in the range. Changeset forwarded.</description></item>
    /// <item><term>Replace</term><description>Callback invoked for the <b>new</b> (replacement) item. Changeset forwarded.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>No callback. Changeset forwarded.</description></item>
    /// <item><term>Moved/Refresh</term><description>No callback. Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>If the callback throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="OnItemRefreshed{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.OnItemAdded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject})"/>
    public static IObservable<IChangeSet<T>> OnItemAdded<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> addAction)
            where T : notnull
        => List.Internal.OnItemAdded<T>.Create(
            source: source,
            addAction: addAction);
}
