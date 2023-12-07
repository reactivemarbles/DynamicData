// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// A simple adaptor to inject side effects into a sorted change set observable.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ISortedChangeSetAdaptor<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Adapts the specified change.
    /// </summary>
    /// <param name="changes">The change.</param>
    void Adapt(ISortedChangeSet<TObject, TKey> changes);
}
