// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Converts moves changes to remove + add.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to convert move events into remove/add pairs.</param>
    /// <returns>the same SortedChangeSets, except all moves are replaced with remove + add.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> TreatMovesAsRemoveAdd<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        static IEnumerable<Change<TObject, TKey>> ReplaceMoves(IChangeSet<TObject, TKey> items)
        {
            foreach (var change in items.ToConcreteType())
            {
                if (change.Reason == ChangeReason.Moved)
                {
                    yield return new Change<TObject, TKey>(ChangeReason.Remove, change.Key, change.Current, change.PreviousIndex);

                    yield return new Change<TObject, TKey>(ChangeReason.Add, change.Key, change.Current, change.CurrentIndex);
                }
                else
                {
                    yield return change;
                }
            }
        }

        return source.Select(changes => new SortedChangeSet<TObject, TKey>(changes.SortedItems, ReplaceMoves(changes)));
    }
}
