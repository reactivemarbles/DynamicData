// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif
/*
 * Object which maintains a sorted list of key value pair and produces a change set.
 *
 * Used by virtualise and page.
 */

/// <summary>
/// Provides members for the SortedKeyValueApplicator class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class SortedKeyValueApplicator<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly Cache<TObject, TKey> _cache = new();

    /// <summary>
    /// The _target field.
    /// </summary>
    private readonly List<KeyValuePair<TKey, TObject>> _target;

    /// <summary>
    /// The _options field.
    /// </summary>
    private readonly SortAndBindOptions _options;

    /// <summary>
    /// The _comparer field.
    /// </summary>
    private KeyValueComparer<TObject, TKey> _comparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedKeyValueApplicator{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="target">The target value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="options">The options value.</param>
    public SortedKeyValueApplicator(List<KeyValuePair<TKey, TObject>> target,
        KeyValueComparer<TObject, TKey> comparer,
        SortAndBindOptions options)
    {
        _target = target;
        _options = options;
        _comparer = comparer;
    }

    /// <summary>
    /// Executes the ChangeComparer operation.
    /// </summary>
    /// <param name="comparer">The comparer value.</param>
    public void ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
    {
        _comparer = comparer;

        _target.Sort(comparer);
    }

    /// <summary>
    /// Executes the ProcessChanges operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
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

    /// <summary>
    /// Executes the Reset operation.
    /// </summary>
    public void Reset()
    {
        var sorted = _cache.KeyValues.OrderBy(t => t, _comparer);
        _target.Clear();
        _target.AddRange(sorted);
    }

    /// <summary>
    /// Executes the ApplyChanges operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
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
                        _target.RemoveAt(currentIndex);

                        var updatedIndex = GetInsertPosition(item);
                        _target.Insert(updatedIndex, item);
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

    /// <summary>
    /// Executes the GetCurrentPosition operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    private int GetCurrentPosition(KeyValuePair<TKey, TObject> item) => _target.GetCurrentPosition(item, _comparer, _options.UseBinarySearch);

    /// <summary>
    /// Executes the GetInsertPosition operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    private int GetInsertPosition(KeyValuePair<TKey, TObject> item) => _target.GetInsertPosition(item, _comparer, _options.UseBinarySearch);
}
