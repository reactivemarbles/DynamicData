// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Linq;
#else

namespace DynamicData.List.Linq;
#endif

/// <summary>
/// Provides members for the UnifiedChangeEnumerator class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="changeSet">The changeSet value.</param>
internal sealed class UnifiedChangeEnumerator<T>(IChangeSet<T> changeSet) : IEnumerable<UnifiedChange<T>>
    where T : notnull
{
    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
