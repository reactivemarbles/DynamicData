// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Experimental;

/// <summary>
/// A specialisation of the SourceList which is optimised for watching individual items.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IWatcher<TObject, TKey> : IDisposable
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Watches updates which match the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>An observable which emits the change.</returns>
    IObservable<Change<TObject, TKey>> Watch(TKey key);
}
