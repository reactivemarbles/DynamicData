// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

using DynamicData.List.Internal;

namespace DynamicData.List.Linq;

internal sealed class UnifiedChangeEnumerator<T>(IChangeSet<T> changeSet) : IEnumerable<UnifiedChange<T>>
    where T : notnull
{
    public IEnumerator<UnifiedChange<T>> GetEnumerator()
    {
        foreach (var change in changeSet)
        {
            if (change.Type == ChangeType.Item)
            {
                yield return new UnifiedChange<T>(change.Reason, change.Item.Current, change.Item.Previous);
            }
            else
            {
                foreach (var item in change.Range)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.AddRange:
                            yield return new UnifiedChange<T>(ListChangeReason.Add, item);
                            break;

                        case ListChangeReason.RemoveRange:
                            yield return new UnifiedChange<T>(ListChangeReason.Remove, item);
                            break;

                        case ListChangeReason.Clear:
                            yield return new UnifiedChange<T>(ListChangeReason.Clear, item);
                            break;

                        default:
                            yield break;
                    }
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
