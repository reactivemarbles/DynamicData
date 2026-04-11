// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// Extension methods for <see cref="KeyedDisposable{TKey}"/>.
/// </summary>
internal static class KeyedDisposableExtensions
{
    /// <summary>
    /// Tracks an item that may or may not be <see cref="IDisposable"/>.
    /// If disposable, replaces any existing entry (disposing the previous if different reference).
    /// If not disposable, removes any existing entry (disposing it).
    /// </summary>
    public static void AddIfDisposable<TKey, TItem>(this KeyedDisposable<TKey> tracker, TKey key, TItem item)
        where TKey : notnull
        where TItem : notnull
    {
        if (item is IDisposable disposable)
            tracker.Add(key, disposable);
        else
            tracker.Remove(key);
    }
}