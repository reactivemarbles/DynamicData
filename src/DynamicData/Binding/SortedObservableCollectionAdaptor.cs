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
/// <param name="resetOnFirstTimeLoad"> Should a reset be fired for a first time load.This option is due to historic reasons where a reset would be fired for the first time load regardless of the number of changes.</param>
public class SortedObservableCollectionAdaptor<TObject, TKey>(int refreshThreshold, bool useReplaceForUpdates = true, bool resetOnFirstTimeLoad = true) : ISortedObservableCollectionAdaptor<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SortedObservableCollectionAdaptor{TObject, TKey}"/> class.
    /// </summary>
    public SortedObservableCollectionAdaptor()
        : this(DynamicDataOptions.Binding)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedObservableCollectionAdaptor{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="options"> The binding options.</param>
    public SortedObservableCollectionAdaptor(BindingOptions options)
        : this(options.ResetThreshold, options.UseReplaceForUpdates, options.ResetOnFirstTimeLoad)
    {
    }

    /// <summary>
    /// Maintains the specified collection from the changes.
    /// </summary>
    /// <param name="changes">The changes.</param>
    /// <param name="collection">The collection.</param>
    public void Adapt(ISortedChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));
        collection.ThrowArgumentNullExceptionIfNull(nameof(collection));

        switch (changes.SortedItems.SortReason)
        {
            case SortReason.ComparerChanged:
            case SortReason.Reset:

                // Multiply items count by 2 as we need to clear existing items
                if (changes.SortedItems.Count * 2 > refreshThreshold)
                {
                    using (collection.SuspendNotifications())
                    {
                        collection.Load(changes.SortedItems.Select(kv => kv.Value));
                    }
                }
                else
                {
                    collection.Load(changes.SortedItems.Select(kv => kv.Value));
                }

                break;

            case SortReason.InitialLoad:
                if (resetOnFirstTimeLoad || (changes.Count - changes.Refreshes > refreshThreshold))
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
