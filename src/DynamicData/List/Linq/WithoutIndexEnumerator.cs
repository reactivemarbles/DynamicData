// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Linq;
#else

namespace DynamicData.List.Linq;
#endif

/// <summary>
/// Removes mutation indices for WhereReasonAre* operators and omits moves.
/// Refresh notifications retain their required source index.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <param name="changeSet">The changeSet value.</param>
internal sealed class WithoutIndexEnumerator<T>(IEnumerable<Change<T>> changeSet) : IEnumerable<Change<T>>
    where T : notnull
{
    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<Change<T>> GetEnumerator()
    {
        foreach (var change in changeSet)
        {
            if (change.Reason == ListChangeReason.Moved)
            {
                // exceptional case - makes no sense to remove index from move
                continue;
            }

            if (change.Reason == ListChangeReason.Refresh)
            {
                yield return change;
            }
            else if (change.Type == ChangeType.Item)
            {
                yield return new Change<T>(change.Reason, change.Item.Current, change.Item.Previous);
            }
            else
            {
                yield return new Change<T>(change.Reason, change.Range);
            }
        }
    }

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
