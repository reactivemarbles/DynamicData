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
    /// Invokes <paramref name="refreshAction"/> for every item with a <see cref="ListChangeReason.Refresh"/> change in the source stream.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe item refresh events in.</param>
    /// <param name="refreshAction">The <see cref="Action{T}"/> action to invoke for each refreshed item.</param>
    /// <returns>A continuation of the source changeset stream, with the side effect applied before forwarding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="refreshAction"/> is <see langword="null"/>.</exception>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.OnItemRefreshed{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{TObject})"/>
    public static IObservable<IChangeSet<T>> OnItemRefreshed<T>(
                this IObservable<IChangeSet<T>> source,
                Action<T> refreshAction)
            where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        return List.Internal.OnItemRefreshed<T>.Create(
                source: source,
                refreshAction: refreshAction);
    }
}
