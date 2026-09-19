// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ManagedGroup class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="groupKey">The groupKey value.</param>
internal sealed class ManagedGroup<TObject, TKey, TGroupKey>(TGroupKey groupKey) : IGroup<TObject, TKey, TGroupKey>, IDisposable
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly IntermediateCache<TObject, TKey> _cache = new();

    /// <summary>
    /// Gets the Cache value.
    /// </summary>
    public IObservableCache<TObject, TKey> Cache => _cache;

    /// <summary>
    /// Gets the Key value.
    /// </summary>
    public TGroupKey Key { get; } = groupKey;

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    internal int Count => _cache.Count;

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _cache.Dispose();

    /// <summary>
    /// Executes the SuspendNotifications operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the GetInitialUpdates operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    internal IChangeSet<TObject, TKey> GetInitialUpdates() => _cache.GetInitialUpdates();

    /// <summary>
    /// Executes the Update operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    internal void Update(Action<ICacheUpdater<TObject, TKey>> updateAction) => _cache.Edit(updateAction);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    private bool Equals(ManagedGroup<TObject, TKey, TGroupKey> other) => EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
}
