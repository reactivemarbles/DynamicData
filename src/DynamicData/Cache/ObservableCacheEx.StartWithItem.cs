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
    /// <inheritdoc cref="StartWithItem{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TObject, TKey)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an initial item to.</param>
    /// <param name="item">The item to prepend. The key is extracted from <see cref="IKey{TKey}.Key"/>.</param>
    /// <remarks>Overload for items that implement <see cref="IKey{TKey}"/>. Delegates to the explicit key overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item)
        where TObject : IKey<TKey>
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWithItem(item, item.Key);
    }

    /// <summary>
    /// Prepends a changeset containing a single <b>Add</b> for the given item and key to the source stream.
    /// The Rx equivalent of <c>StartWith</c>, but wrapped as a DynamicData changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an initial item to.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to prepend.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key for the item.</param>
    /// <returns>An observable that emits a single-item Add changeset first, then all source changesets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        var change = new Change<TObject, TKey>(ChangeReason.Add, key, item);
        return source.StartWith(new ChangeSet<TObject, TKey> { change });
    }
}
