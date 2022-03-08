// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal;

internal sealed class ManagedGroup<TObject, TKey, TGroupKey> : IGroup<TObject, TKey, TGroupKey>, IDisposable
    where TKey : notnull
{
    private readonly IntermediateCache<TObject, TKey> _cache = new();

    public ManagedGroup(TGroupKey groupKey)
    {
        Key = groupKey;
    }

    public IObservableCache<TObject, TKey> Cache => _cache;

    public TGroupKey Key { get; }

    internal int Count => _cache.Count;

    public void Dispose()
    {
        _cache.Dispose();
    }

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// true if the specified <see cref="object"/> is equal to the current <see cref="object"/>; otherwise, false.
    /// </returns>
    /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="object"/>. </param>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ManagedGroup<TObject, TKey, TGroupKey> managedGroup && Equals(managedGroup);
    }

    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>
    /// A hash code for the current <see cref="object"/>.
    /// </returns>
    public override int GetHashCode()
    {
        return Key is null ? 0 : EqualityComparer<TGroupKey>.Default.GetHashCode(Key);
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="object"/>.
    /// </returns>
    public override string ToString()
    {
        return $"Group: {Key}";
    }

    internal IChangeSet<TObject, TKey> GetInitialUpdates()
    {
        return _cache.GetInitialUpdates();
    }

    internal void Update(Action<ICacheUpdater<TObject, TKey>> updateAction)
    {
        _cache.Edit(updateAction);
    }

    private bool Equals(ManagedGroup<TObject, TKey, TGroupKey> other)
    {
        return EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
    }
}
