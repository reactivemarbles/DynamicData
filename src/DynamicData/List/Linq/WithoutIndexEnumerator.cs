// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
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
    public IEnumerator<Change<T>> GetEnumerator()
    {
        foreach (var change in changeSet)
        {
            if (change.Reason == ListChangeReason.Moved)
            {
                // exceptional case - makes no sense to remove index from move
                continue;
            }

            if (change.Type == ChangeType.Item)
            {
                yield return new Change<T>(change.Reason, change.Item.Current, change.Item.Previous);
            }
            else
            {
                yield return new Change<T>(change.Reason, change.Range);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
