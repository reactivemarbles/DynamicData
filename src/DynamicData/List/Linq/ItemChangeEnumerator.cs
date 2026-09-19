// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Linq;
#else

namespace DynamicData.List.Linq;
#endif

/// <summary>
/// Provides members for the ItemChangeEnumerator class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="changeSet">The changeSet value.</param>
internal sealed class ItemChangeEnumerator<T>(IChangeSet<T> changeSet) : IEnumerable<ItemChange<T>>
    where T : notnull
{
    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<ItemChange<T>> GetEnumerator()
    {
        var lastKnownIndex = 0;

        foreach (var change in changeSet)
        {
            if (change.Type == ChangeType.Item)
            {
                lastKnownIndex = change.Item.CurrentIndex;
                yield return new ItemChange<T>(change.Reason, change.Item.Current, change.Item.Previous, change.Item.CurrentIndex, change.Item.PreviousIndex);
            }
            else
            {
                var index = change.Range.Index == -1 ? lastKnownIndex : change.Range.Index;

                foreach (var item in change.Range)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.AddRange:
                            yield return new ItemChange<T>(ListChangeReason.Add, item, index);
                            break;

                        case ListChangeReason.RemoveRange:
                        case ListChangeReason.Clear:
                            yield return new ItemChange<T>(ListChangeReason.Remove, item, index);
                            break;

                        default:
                            yield break;
                    }

                    index++;
                    lastKnownIndex = index;
                }
            }
        }
    }

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
