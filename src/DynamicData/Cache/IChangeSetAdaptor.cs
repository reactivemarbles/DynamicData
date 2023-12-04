// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace DynamicData;

/// <summary>
/// A simple adaptor to inject side effects into a change set observable.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IChangeSetAdaptor<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Adapts the specified change.
    /// </summary>
    /// <param name="changes">The change.</param>
    void Adapt(IChangeSet<TObject, TKey> changes);
}
