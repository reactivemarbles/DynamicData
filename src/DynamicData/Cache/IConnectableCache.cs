// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace DynamicData;

/// <summary>
/// A cache for observing and querying in memory data.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IConnectableCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets a count changed observable starting with the current count.
    /// </summary>
    IObservable<int> CountChanged { get; }

    /// <summary>
    /// Returns a filtered stream of cache changes preceded with the initial filtered state.
    /// </summary>
    /// <param name="predicate">The result will be filtered using the specified predicate.</param>
    /// <param name="suppressEmptyChangeSets">By default, empty change sets are not emitted. Set this value to false to emit empty change sets.</param>
    /// <returns>An observable that emits the change set.</returns>
    IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true);

    /// <summary>
    /// Returns a filtered stream of cache changes.
    /// Unlike Connect(), the returned observable is not prepended with the caches initial items.
    /// </summary>
    /// <param name="predicate">The result will be filtered using the specified predicate.</param>
    /// <returns>An observable that emits the change set.</returns>
    IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null);

    /// <summary>
    /// Returns an observable of any changes which match the specified key.  The sequence starts with the initial item in the cache (if there is one).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>An observable that emits the change set.</returns>
    IObservable<Change<TObject, TKey>> Watch(TKey key);
}
