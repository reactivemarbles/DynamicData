// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Provides a filter.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the field.</typeparam>
internal interface IFilter<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets the filter to use.
    /// </summary>
    Func<TObject, bool> Filter { get; }

    /// <summary>
    /// Provides a change set with refreshed items.
    /// </summary>
    /// <param name="items">The items to refresh.</param>
    /// <returns>A change set of the changes.</returns>
    IChangeSet<TObject, TKey> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items);

    /// <summary>
    /// Provides a change set with updated items.
    /// </summary>
    /// <param name="updates">The items to update.</param>
    /// <returns>A change set of the changes.</returns>
    IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates);
}
