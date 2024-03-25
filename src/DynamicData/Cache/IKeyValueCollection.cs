// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// A key collection which contains sorting information.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IKeyValueCollection<TObject, TKey> : IReadOnlyList<KeyValuePair<TKey, TObject>>
{
    /// <summary>
    /// Gets the comparer used to perform the sort.
    /// </summary>
    /// <value>
    /// The comparer.
    /// </value>
    IComparer<KeyValuePair<TKey, TObject>> Comparer { get; }

    /// <summary>
    /// Gets the optimisations used to produce the sort.
    /// </summary>
    /// <value>
    /// The optimisations.
    /// </value>
    SortOptimisations Optimisations { get; }

    /// <summary>
    /// Gets the reason for a sort being applied.
    /// </summary>
    /// <value>
    /// The sort reason.
    /// </value>
    SortReason SortReason { get; }
}
