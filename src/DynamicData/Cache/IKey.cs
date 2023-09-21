// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Represents the key of an object.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IKey<out T>
{
    /// <summary>
    /// Gets the key.
    /// </summary>
    T Key { get; }
}
