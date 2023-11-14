// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// Represents an adaptor which is used to update observable collection from
/// a sorted change set stream.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SortedObservableCollectionAdaptor{TObject, TKey}"/> class.
/// </remarks>
/// <param name="refreshThreshold">The number of changes before a Reset event is used.</param>
/// <param name="useReplaceForUpdates"> Use replace instead of remove / add for updates. </param>
public class SortedObservableCollectionAdaptor<TObject, TKey>(int refreshThreshold = 25, bool useReplaceForUpdates = true) : ISortedObservableCollectionAdaptor<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Maintains the specified collection from the changes.
    /// </summary>
    /// <param name="changes">The changes.</param>
    /// <param name="collection">The collection.</param>
    public void Adapt(ISortedChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection)
    {
        if (changes is null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        switch (changes.SortedItems.SortReason)
        {
            case SortReason.InitialLoad:
            case SortReason.ComparerChanged:
            case SortReason.Reset:
                using (collection.SuspendNotifications())
                {
                    collection.Load(changes.SortedItems.Select(kv => kv.Value));
                }

                break;

            case SortReason.DataChanged:
                if (changes.Count - changes.Refreshes > refreshThreshold)
                {
                    using (collection.SuspendNotifications())
                    {
                        collection.Load(changes.SortedItems.Select(kv => kv.Value));
                    }
                }
                else
                {
                    using (collection.SuspendCount())
                    {
                        DoUpdate(changes, collection);
                    }
                }

                break;

            case SortReason.Reorder:
                // Updates will only be moves, so apply logic
                using (collection.SuspendCount())
                {
                    DoUpdate(changes, collection);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changes));
        }
    }

    private void DoUpdate(ISortedChangeSet<TObject, TKey> updates, IObservableCollection<TObject> list)
    {
        foreach (var update in updates)
        {
            switch (update.Reason)
            {
                case ChangeReason.Add:
                    list.Insert(update.CurrentIndex, update.Current);
                    break;

                case ChangeReason.Remove:
                    list.RemoveAt(update.CurrentIndex);
                    break;

                case ChangeReason.Moved:
                    list.Move(update.PreviousIndex, update.CurrentIndex);
                    break;

                case ChangeReason.Update:
                    if (!useReplaceForUpdates || update.PreviousIndex != update.CurrentIndex)
                    {
                        list.RemoveAt(update.PreviousIndex);
                        list.Insert(update.CurrentIndex, update.Current);
                    }
                    else
                    {
                        list[update.CurrentIndex] = update.Current;
                    }

                    break;
            }
        }
    }
}
