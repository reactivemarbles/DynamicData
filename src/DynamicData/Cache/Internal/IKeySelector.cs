// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Selects a key from a item.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
internal interface IKeySelector<in TObject, out TKey> // : IKeySelector<TObject>
{
    /// <summary>
    /// Gets the key from the object.
    /// </summary>
    /// <param name="item">The item to get the key for.</param>
    /// <returns>The key.</returns>
    TKey GetKey(TObject item);
}
