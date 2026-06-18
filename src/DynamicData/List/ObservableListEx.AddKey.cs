// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Adds a key to each item in a list changeset, converting it to a cache changeset that supports all keyed DynamicData operators.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to add keys to, converting to a cache changeset.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> function to extract a unique key from each item.</param>
    /// <returns>A cache <see cref="IObservable{IChangeSet{TObject, TKey}}"/> changeset stream with keyed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All index information is dropped during conversion because cache changesets are unordered by default.
    /// Use this when you need to transition from list-based pipelines to cache-based operators (Filter by key, Join, Group, etc.).
    /// </para>
    /// </remarks>
    /// <seealso cref="ObservableCacheEx.RemoveKey{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
    }
}
