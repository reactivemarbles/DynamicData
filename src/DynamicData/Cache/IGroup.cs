// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace DynamicData;

/// <summary>
/// An update stream which has been grouped by a common key.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TGroupKey">The type of value used to group the original stream.</typeparam>
public interface IGroup<TObject, TKey, out TGroupKey> : IKey<TGroupKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets the observable for the group.
    /// </summary>
    /// <value>
    /// The observable.
    /// </value>
    IObservableCache<TObject, TKey> Cache { get; }
}
