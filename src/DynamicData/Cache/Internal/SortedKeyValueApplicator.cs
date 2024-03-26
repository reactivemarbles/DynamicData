// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace DynamicData.Cache.Internal;

/*
 * Object which maintains a sorted list of key value pair and produces a change set.
 *
 * Used by virtualise and page.
 */
internal sealed class SortedKeyValueApplicator<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Cache<TObject, TKey> _cache = new();
    private readonly List<KeyValuePair<TKey, TObject>> _target;
    private readonly SortAndBindOptions _options;

    private KeyValueComparer<TObject, TKey> _comparer;

    public SortedKeyValueApplicator(List<KeyValuePair<TKey, TObject>> target,
        KeyValueComparer<TObject, TKey> comparer,
        SortAndBindOptions options)
    {
        _target = target;
        _options = options;
        _comparer = comparer;
    }

    public void ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
    {
        _comparer = comparer;

        _target.Sort(comparer);
    }

    public void ProcessChanges(IChangeSet<TObject, TKey> changes)
    {
        _cache.Clone(changes);

        var fireReset = _options.ResetThreshold > 0 && _options.ResetThreshold < changes.Count;

        if (fireReset)
        {
            Reset();
        }
        else
        {
            ApplyChanges(changes);
        }
    }

    public void Reset()
    {
        var sorted = _cache.KeyValues.OrderBy(t => t, _comparer);
        _target.Clear();
        _target.AddRange(sorted);
    }

    private void ApplyChanges(IChangeSet<TObject, TKey> changes)
    {
        // iterate through collection, find sorted position and apply changes
        foreach (var change in changes.ToConcreteType())
        {
            var item = new KeyValuePair<TKey, TObject>(change.Key, change.Current);

            switch (change.Reason)
            {
                case ChangeReason.Add:
                    {
                        var index = GetInsertPosition(item);
                        _target.Insert(index, item);
                    }
                    break;
                case ChangeReason.Update:
                    {
                        var previous = new KeyValuePair<TKey, TObject>(change.Key, change.Previous.Value);
                        var currentIndex = GetCurrentPosition(previous);
                        var updatedIndex = GetInsertPosition(item);

                        // We need to recalibrate as GetCurrentPosition includes the current item
                        updatedIndex = currentIndex < updatedIndex ? updatedIndex - 1 : updatedIndex;

                        // Some control suites and platforms do not support replace, whiles others do, so we opt in.
                        if (_options.UseReplaceForUpdates && currentIndex == updatedIndex)
                        {
                            _target[currentIndex] = item;
                        }
                        else
                        {
                            _target.RemoveAt(currentIndex);
                            _target.Insert(updatedIndex, item);
                        }
                    }
                    break;
                case ChangeReason.Remove:
                    {
                        var currentIndex = GetCurrentPosition(item);
                        _target.RemoveAt(currentIndex);
                    }
                    break;
                case ChangeReason.Refresh:
                    {
                        /*  look up current location, and new location
                         *
                         *  Use the linear methods as binary search does not work if we do not have an already sorted list.
                         *  Otherwise, SortAndBindWithBinarySearch.Refresh() unit test will break.
                         *
                         * If consumers are using BinarySearch and a refresh event is sent here, they probably should exclude refresh
                         * events with .WhereReasonsAreNot(ChangeReason.Refresh), but it may be problematic to exclude refresh automatically
                         * as that would effectively be swallowing an error.
                         */
                        var currentIndex = _target.IndexOf(item);
                        var updatedIndex = _target.GetInsertPositionLinear(item, _comparer);

                        // We need to recalibrate as GetInsertPosition includes the current item
                        updatedIndex = currentIndex < updatedIndex ? updatedIndex - 1 : updatedIndex;
                        if (updatedIndex != currentIndex)
                        {
                            _target.RemoveAt(currentIndex);
                            _target.Insert(updatedIndex, item);
                        }
                    }
                    break;
                case ChangeReason.Moved:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private int GetCurrentPosition(KeyValuePair<TKey, TObject> item) => _target.GetCurrentPosition(item, _comparer, _options.UseBinarySearch);

    private int GetInsertPosition(KeyValuePair<TKey, TObject> item) => _target.GetInsertPosition(item, _comparer, _options.UseBinarySearch);
}
