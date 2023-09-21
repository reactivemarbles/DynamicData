// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// A keyed value.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IKeyValue<out TObject, out TKey> : IKey<TKey>
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    TObject Value { get; }
}
