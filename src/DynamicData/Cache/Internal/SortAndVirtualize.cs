// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Binding;

namespace DynamicData.Cache.Internal;

internal sealed class SortAndVirtualize<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source,
    IComparer<TObject> comparer,
    IObservable<IVirtualRequest> virtualRequests,
    SortAndVirtualizeOptions options)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly IObservable<IVirtualRequest> _virtualRequests = virtualRequests ?? throw new ArgumentNullException(nameof(virtualRequests));
    private static readonly KeyComparer<TObject, TKey> KeyComparer = new();

    public IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> Run() =>
        Observable.Create<IChangeSet<TObject, TKey, VirtualContext<TObject>>>(
            observer =>
            {
                var locker = new object();

                IVirtualRequest virtualParams = VirtualRequest.Default;

                var sortedList = new List<KeyValuePair<TKey, TObject>>(options.InitialCapacity);
                var virtualItems = new List<KeyValuePair<TKey, TObject>>(virtualParams.Size);
                var keyValueComparer = new KeyValueComparer<TObject, TKey>(comparer);

                var sortOptions = new SortAndBindOptions
                {
                    UseBinarySearch = options.UseBinarySearch,
                    ResetThreshold = options.ResetThreshold
                };

                var applicator = new SortedKeyValueApplicator<TObject, TKey>(sortedList, keyValueComparer, sortOptions);

                var paramsChanged = _virtualRequests.Synchronize(locker)
                    .DistinctUntilChanged()
                    // exclude dodgy params
                    .Where(parameters => parameters is { StartIndex: >= 0, Size: > 0 })
                    .Select(request =>
                    {
                        virtualParams = request;

                        // re-apply virtual changes
                        return ApplyVirtualChanges();
                    });

                var dataChange = _source.Synchronize(locker)
                    // we need to ensure each change batch has unique keys only.
                    // Otherwise, calculation of virtualized changes is super complex
                    .EnsureUniqueKeys()
                    .Select(changes =>
                    {
                        // apply changes to the sorted list
                        applicator.ProcessChanges(changes);

                        // re-apply virtual changes
                        return ApplyVirtualChanges(changes);
                    });

                return paramsChanged
                    .Merge(dataChange)
                    .Where(changes => changes.Count is not 0)
                    .SubscribeSafe(observer);

                ChangeSet<TObject, TKey, VirtualContext<TObject>> ApplyVirtualChanges(IChangeSet<TObject, TKey>? changeSet = null)
                {
                    // re-calculate virtual changes
                    var currentVirtualItems = new List<KeyValuePair<TKey, TObject>>(virtualParams.Size);
                    currentVirtualItems.AddRange(sortedList.Skip(virtualParams.StartIndex).Take(virtualParams.Size));

                    var responseParams = new VirtualResponse(virtualParams.Size, virtualParams.StartIndex, sortedList.Count);
                    var context = new VirtualContext<TObject>(responseParams, comparer, options);

                    // calculate notifications
                    var virtualChanges = CalculateVirtualChanges(context, currentVirtualItems, virtualItems, changeSet);

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

        var removes = previousItems.Except(currentItems, KeyComparer).Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
        var adds = currentItems.Except(previousItems, KeyComparer).Select(kvp => new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));

        result.AddRange(removes);
        result.AddRange(adds);

        if (changes is null) return result;

        var keyInPreviousAndCurrent = new HashSet<TKey>(previousItems.Intersect(currentItems, KeyComparer).Select(x => x.Key));

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
