// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
///  A grouped update collection.
/// </summary>
/// <typeparam name="TObject">The source object type.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>s
/// <typeparam name="TGroupKey">The value on which the stream has been grouped.</typeparam>
public interface IImmutableGroupChangeSet<TObject, TKey, TGroupKey> : IChangeSet<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
}
