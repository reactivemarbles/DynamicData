// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace DynamicData.List.Linq;

/// <summary>
/// Index to remove the index. This is necessary for WhereReasonAre* operators.
/// Otherwise these operators could break subsequent operators when the subsequent operator relies on the index.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
internal sealed class WithoutIndexEnumerator<T>(IEnumerable<Change<T>> changeSet) : IEnumerable<Change<T>>
    where T : notnull
{
    /// <summary>
    /// Copies an Add, AddRange, Replace, Remove, RemoveRange or Clear change without its indexes.
    /// </summary>
    /// <param name="change">The change to copy. Moved and Refresh changes cannot be unindexed.</param>
    /// <returns>The unindexed copy.</returns>
    public static Change<T> WithoutIndex(Change<T> change)
        => (change.Type == ChangeType.Item)
            ? new Change<T>(change.Reason, change.Item.Current, change.Item.Previous)
            : new Change<T>(change.Reason, change.Range);

    public IEnumerator<Change<T>> GetEnumerator()
    {
        foreach (var change in changeSet)
        {
            if (change.Reason == ListChangeReason.Moved)
            {
                // exceptional case - makes no sense to remove index from move
                continue;
            }

            yield return WithoutIndex(change);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
