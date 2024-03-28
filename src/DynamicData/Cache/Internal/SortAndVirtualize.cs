// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Binding;

namespace DynamicData.Cache.Internal;

internal sealed class SortAndVirtualize<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private static readonly KeyComparer<TObject, TKey> _keyComparer = new();

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;
    private readonly IObservable<IComparer<TObject>> _comparerChanged;
    private readonly IObservable<IVirtualRequest> _virtualRequests;
    private readonly SortAndVirtualizeOptions _options;

    public SortAndVirtualize(IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IVirtualRequest> virtualRequests,
        SortAndVirtualizeOptions options)
     : this(source, Observable.Return(comparer), virtualRequests, options)
    {
    }

    public SortAndVirtualize(IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        IObservable<IVirtualRequest> virtualRequests,
        SortAndVirtualizeOptions options)
    {
        _options = options;
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _comparerChanged = comparerChanged;
        _virtualRequests = virtualRequests ?? throw new ArgumentNullException(nameof(virtualRequests));
    }

    private static readonly ChangeSet<TObject, TKey, VirtualContext<TObject>> Empty = new(0, VirtualContext<TObject>.Empty);

    public IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> Run() =>
        Observable.Create<IChangeSet<TObject, TKey, VirtualContext<TObject>>>(
            observer =>
            {
                var locker = new object();

                var sortOptions = new SortAndBindOptions
                {
                    UseBinarySearch = _options.UseBinarySearch,
                    ResetThreshold = _options.ResetThreshold
                };

                IVirtualRequest virtualParams = VirtualRequest.Default;

                // a sorted list of key value pairs, maintained by
                var sortedList = new List<KeyValuePair<TKey, TObject>>(_options.InitialCapacity);
                var virtualItems = new List<KeyValuePair<TKey, TObject>>(virtualParams.Size);

                IComparer<TObject>? comparer = null;
                KeyValueComparer<TObject, TKey>? keyValueComparer = null;
                SortedKeyValueApplicator<TObject, TKey>? applicator = null;

                // used to maintain a sorted list of key value pairs
                var comparerChanged = _comparerChanged.Synchronize(locker)
                    .Select(c =>
                    {
                        comparer = c;
                        keyValueComparer = new KeyValueComparer<TObject, TKey>(c);

                        if (applicator is null)
                        {
                            applicator = new SortedKeyValueApplicator<TObject, TKey>(sortedList, keyValueComparer, sortOptions);
                        }
                        else
                        {
                            applicator.ChangeComparer(keyValueComparer);
                        }
                        return ApplyVirtualChanges();
                    });

                var paramsChanged = _virtualRequests.Synchronize(locker)
                    .DistinctUntilChanged()
                    // exclude dodgy params
                    .Where(parameters => parameters is { StartIndex: >= 0, Size: > 0 })
                    .Select(request =>
                    {
                        virtualParams = request;

                        // have not received the comparer yet
                        if (applicator is null) return Empty;

                        // re-apply virtual changes
                        return ApplyVirtualChanges();
                    });

                var dataChange = _source.Synchronize(locker)
                    // we need to ensure each change batch has unique keys only.
                    // Otherwise, calculation of virtualized changes is super complex
                    .EnsureUniqueKeys()
                    .Select(changes =>
                    {
                        // have not received the comparer yet
                        if (applicator is null) return Empty;

                        // apply changes to the sorted list
                        applicator.ProcessChanges(changes);

                        // re-apply virtual changes
                        return ApplyVirtualChanges(changes);
                    });

                return
                    comparerChanged
                        .Merge(paramsChanged)
                        .Merge(dataChange)
                        .Where(changes => changes.Count is not 0)
                        .SubscribeSafe(observer);

                ChangeSet<TObject, TKey, VirtualContext<TObject>> ApplyVirtualChanges(IChangeSet<TObject, TKey>? changeSet = null)
                {
                    var previousVirtualList = virtualItems;

                    // re-calculate virtual changes
                    var currentVirtualItems = new List<KeyValuePair<TKey, TObject>>(virtualParams.Size);
                    currentVirtualItems.AddRange(sortedList.Skip(virtualParams.StartIndex).Take(virtualParams.Size));

                    var responseParams = new VirtualResponse(virtualParams.Size, virtualParams.StartIndex, sortedList.Count);
                    var context = new VirtualContext<TObject>(responseParams, comparer!, _options);

                    // calculate notifications
                    var virtualChanges = CalculateVirtualChanges(context, currentVirtualItems, previousVirtualList, changeSet);

                    virtualItems = currentVirtualItems;

                    return virtualChanges;
                }
            });

    // Calculates any changes within the virtualized range.
    private static ChangeSet<TObject, TKey, VirtualContext<TObject>> CalculateVirtualChanges(VirtualContext<TObject> context,
        List<KeyValuePair<TKey, TObject>> currentItems,
        List<KeyValuePair<TKey, TObject>> previousItems,
        IChangeSet<TObject, TKey>? changes = null)
    {
        var result = new ChangeSet<TObject, TKey, VirtualContext<TObject>>(currentItems.Count * 2, context);

        var removes = previousItems.Except(currentItems, _keyComparer).Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
        var adds = currentItems.Except(previousItems, _keyComparer).Select(kvp => new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));

        result.AddRange(removes);
        result.AddRange(adds);

        if (changes is null) return result;

        var keyInPreviousAndCurrent = new HashSet<TKey>(previousItems.Intersect(currentItems, _keyComparer).Select(x => x.Key));

        foreach (var change in changes)
        {
            // An update (or refresh) can only occur if it was in the previous or current result set.
            // If it was in only one or the other, it would be an add or remove accordingly.
            if (!keyInPreviousAndCurrent.Contains(change.Key)) continue;

            if (change.Reason is ChangeReason.Update or ChangeReason.Refresh)
            {
                result.Add(change);
            }
        }

        return result;
    }
}
