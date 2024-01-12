// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

internal sealed class ChangeSetMergeTracker<TObject>
    where TObject : notnull
{
    private readonly ChangeAwareList<TObject> _resultList = new();

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

    public void RemoveItems(IEnumerable<TObject> removeItems, IObserver<IChangeSet<TObject>>? observer = null)
    {
        _resultList.Remove(removeItems);

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void EmitChanges(IObserver<IChangeSet<TObject>> observer)
    {
        var changeSet = _resultList.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(changeSet);
        }
    }

    private void OnClear(Change<TObject> change) => _resultList.ClearOrRemoveMany(change);

    private void OnItemAdded(ItemChange<TObject> item) => _resultList.Add(item.Current);

    private void OnItemRefreshed(ItemChange<TObject> item) => _resultList.Refresh(item.Current);

    private void OnItemRemoved(ItemChange<TObject> item) => _resultList.Remove(item.Current);

    private void OnItemReplaced(ItemChange<TObject> item) => _resultList.ReplaceOrAdd(item.Previous.Value, item.Current);

    private void OnRangeAdded(RangeChange<TObject> range) => _resultList.AddRange(range);

    private void OnRangeRemoved(RangeChange<TObject> range) => _resultList.Remove(range);
}
