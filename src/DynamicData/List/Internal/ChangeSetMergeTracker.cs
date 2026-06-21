// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ChangeSetMergeTracker class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
internal sealed class ChangeSetMergeTracker<TObject>
    where TObject : notnull
{
    /// <summary>
    /// The _resultList field.
    /// </summary>
    private readonly ChangeAwareList<TObject> _resultList = new();

    /// <summary>
    /// Executes the ProcessChangeSet operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <param name="observer">The observer value.</param>
    public void ProcessChangeSet(IChangeSet<TObject> changes, IObserver<IChangeSet<TObject>>? observer = null)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    OnItemAdded(change.Item);
                    break;

                case ListChangeReason.Remove:
                    OnItemRemoved(change.Item);
                    break;

                case ListChangeReason.Replace:
                    OnItemReplaced(change.Item);
                    break;

                case ListChangeReason.Refresh:
                    OnItemRefreshed(change.Item);
                    break;

                case ListChangeReason.AddRange:
                    OnRangeAdded(change.Range);
                    break;

                case ListChangeReason.RemoveRange:
                    OnRangeRemoved(change.Range);
                    break;

                case ListChangeReason.Clear:
                    OnClear(change);
                    break;

                case ListChangeReason.Moved:
                    // Ignore move changes because nothing can be done due to the indexes being different in the merged result
                    break;
            }
        }

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    /// <summary>
    /// Executes the RemoveItems operation.
    /// </summary>
    /// <param name="removeItems">The removeItems value.</param>
    /// <param name="observer">The observer value.</param>
    public void RemoveItems(IEnumerable<TObject> removeItems, IObserver<IChangeSet<TObject>>? observer = null)
    {
        _resultList.Remove(removeItems);

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    /// <summary>
    /// Executes the EmitChanges operation.
    /// </summary>
    /// <param name="observer">The observer value.</param>
    public void EmitChanges(IObserver<IChangeSet<TObject>> observer)
    {
        var changeSet = _resultList.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(changeSet);
        }
    }

    /// <summary>
    /// Executes the OnClear operation.
    /// </summary>
    /// <param name="change">The change value.</param>
    private void OnClear(Change<TObject> change) => _resultList.ClearOrRemoveMany(change);

    /// <summary>
    /// Executes the OnItemAdded operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    private void OnItemAdded(ItemChange<TObject> item) => _resultList.Add(item.Current);

    /// <summary>
    /// Executes the OnItemRefreshed operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    private void OnItemRefreshed(ItemChange<TObject> item) => _resultList.Refresh(item.Current);

    /// <summary>
    /// Executes the OnItemRemoved operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    private void OnItemRemoved(ItemChange<TObject> item) => _resultList.Remove(item.Current);

    /// <summary>
    /// Executes the OnItemReplaced operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    private void OnItemReplaced(ItemChange<TObject> item) => _resultList.ReplaceOrAdd(item.Previous.Value, item.Current);

    /// <summary>
    /// Executes the OnRangeAdded operation.
    /// </summary>
    /// <param name="range">The range value.</param>
    private void OnRangeAdded(RangeChange<TObject> range) => _resultList.AddRange(range);

    /// <summary>
    /// Executes the OnRangeRemoved operation.
    /// </summary>
    /// <param name="range">The range value.</param>
    private void OnRangeRemoved(RangeChange<TObject> range) => _resultList.Remove(range);
}
