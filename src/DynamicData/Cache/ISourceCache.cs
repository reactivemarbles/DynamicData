// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// An observable cache which exposes an update API.  Used at the root
/// of all observable chains.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ISourceCache<TObject, TKey> : IObservableCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets key selector used by the cache to retrieve keys from objects.
    /// </summary>
    Func<TObject, TKey> KeySelector { get; }

    /// <summary>
    /// Action to apply a batch update to a cache. Multiple update methods can be invoked within a single batch operation.
    /// These operations are invoked within the cache's lock and is therefore thread safe.
    /// The result of the action will produce a single change set.
    /// </summary>
    /// <param name="updateAction">The update action.</param>
    void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction);
}
