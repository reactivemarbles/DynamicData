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
    /// Invokes <paramref name="action"/> once for every <see cref="Change{T}"/> in each changeset. Range changes
    /// (AddRange, RemoveRange, Clear) are delivered as a single <see cref="Change{T}"/>; they are not flattened into per-item changes.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe each change in.</param>
    /// <param name="action">The action invoked for each <see cref="Change{T}"/>.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a side-effect operator. It does not modify the changeset. If you need each individual item from range operations flattened out, use <see cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/> instead.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/Replace/Remove/Moved/Refresh</term><description>Callback invoked with the <see cref="Change{T}"/> (single-item change). Changeset forwarded.</description></item>
    /// <item><term>AddRange/RemoveRange/Clear</term><description>Callback invoked once with the <see cref="Change{T}"/> containing the range (accessible via <c>Range</c> property). Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>If the callback throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="ObservableCacheEx.ForEachChange{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{Change{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<Change<TObject>> action)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(changes => changes.ForEach(action));
    }
}
