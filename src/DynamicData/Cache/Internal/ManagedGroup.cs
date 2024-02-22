// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class ManagedGroup<TObject, TKey, TGroupKey>(TGroupKey groupKey) : IGroup<TObject, TKey, TGroupKey>, IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly IntermediateCache<TObject, TKey> _cache = new();

    public IObservableCache<TObject, TKey> Cache => _cache;

    public TGroupKey Key { get; } = groupKey;

    internal int Count => _cache.Count;

    public void Dispose() => _cache.Dispose();

    public IDisposable SuspendNotifications() => _cache.SuspendNotifications();

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// true if the specified <see cref="object"/> is equal to the current <see cref="object"/>; otherwise, false.
    /// </returns>
    /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="object"/>. </param>
    public override bool Equals(object? obj)
    {
        if (obj is null)
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
    public override int GetHashCode() => Key is null ? 0 : EqualityComparer<TGroupKey>.Default.GetHashCode(Key);

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="object"/>.
    /// </returns>
    public override string ToString() => $"Group: {Key}";

    internal IChangeSet<TObject, TKey> GetInitialUpdates() => _cache.GetInitialUpdates();

    internal void Update(Action<ICacheUpdater<TObject, TKey>> updateAction) => _cache.Edit(updateAction);

    private bool Equals(ManagedGroup<TObject, TKey, TGroupKey> other) => EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
}
