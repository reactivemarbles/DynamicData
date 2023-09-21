// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Represents a subset of data reduced by a defined set of parameters.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IVirtualChangeSet<TObject, TKey> : ISortedChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets the parameters used to virtualise the stream.
    /// </summary>
    IVirtualResponse Response { get; }
}
