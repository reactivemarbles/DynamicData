// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache;

/// <summary>
/// Implements a Cache Updater that doesn't make changes until Disposed.
/// </summary>
/// <typeparam name="TObject">Type of the cache contents.</typeparam>
/// <typeparam name="TKey">Type of the cache key.</typeparam>
internal interface IDeferredCacheUpdater<TObject, TKey> : ICacheUpdater<TObject, TKey>, IDisposable
    where TObject : notnull
    where TKey : notnull
{
}
