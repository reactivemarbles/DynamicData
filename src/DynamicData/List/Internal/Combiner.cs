// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Cache.Internal;

namespace DynamicData.List.Internal;

internal sealed class Combiner<T>(ICollection<IObservable<IChangeSet<T>>> source, CombineOperator type)
    where T : notnull
{
    private readonly object _locker = new();

    private readonly ICollection<IObservable<IChangeSet<T>>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var disposable = new CompositeDisposable();

                var resultList = new ChangeAwareListWithRefCounts<T>();

                lock (_locker)
                {
                    var sourceLists = Enumerable.Range(0, _source.Count).Select(_ => new ReferenceCountTracker<T>()).ToList();

                    foreach (var pair in _source.Zip(sourceLists, (item, list) => new { Item = item, List = list }))
                    {
                        disposable.Add(
                            pair.Item.Synchronize(_locker).Subscribe(
                                changes =>
                                {
                                    CloneSourceList(pair.List, changes);

                                    var notifications = UpdateResultList(changes, sourceLists, resultList);
                                    if (notifications.Count != 0)
                                    {
                                        observer.OnNext(notifications);
                                    }
                                }));
                    }
                }

                return disposable;
            });

    private static void CloneSourceList(ReferenceCountTracker<T> tracker, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    tracker.Add(change.Item.Current);
                    break;

                case ListChangeReason.AddRange:
                    foreach (var t in change.Range)
                    {
                        tracker.Add(t);
                    }

                    break;

                case ListChangeReason.Replace:
                    tracker.Remove(change.Item.Previous.Value);
                    tracker.Add(change.Item.Current);
                    break;

                case ListChangeReason.Remove:
                    tracker.Remove(change.Item.Current);
                    break;

                case ListChangeReason.RemoveRange:
                case ListChangeReason.Clear:
                    foreach (var t in change.Range)
                    {
                        tracker.Remove(t);
                    }

                    break;
            }
        }
    }

    private bool MatchesConstraint(List<ReferenceCountTracker<T>> sourceLists, T item)
    {
        switch (type)
        {
            case CombineOperator.And:
                {
                    return sourceLists.All(s => s.Contains(item));
                }

            case CombineOperator.Or:
                {
                    return sourceLists.Any(s => s.Contains(item));
                }

            case CombineOperator.Xor:
                {
                    return sourceLists.Count(s => s.Contains(item)) == 1;
                }

            case CombineOperator.Except:
                {
                    var first = sourceLists[0].Contains(item);
                    var others = sourceLists.Skip(1).Any(s => s.Contains(item));
                    return first && !others;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    private void UpdateItemMembership(T item, List<ReferenceCountTracker<T>> sourceLists, ChangeAwareListWithRefCounts<T> resultList)
    {
        var isInResult = resultList.Contains(item);
        var shouldBeInResult = MatchesConstraint(sourceLists, item);
        if (shouldBeInResult && !isInResult)
        {
            resultList.Add(item);
        }
        else if (!shouldBeInResult && isInResult)
        {
            resultList.Remove(item);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "By Design.")]
    private IChangeSet<T> UpdateResultList(IChangeSet<T> changes, List<ReferenceCountTracker<T>> sourceLists, ChangeAwareListWithRefCounts<T> resultList)
    {
        // child caches have been updated before we reached this point.
        foreach (var change in changes.Flatten())
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                case ListChangeReason.Remove:
                    UpdateItemMembership(change.Current, sourceLists, resultList);
                    break;

                case ListChangeReason.Replace:
                    UpdateItemMembership(change.Previous.Value, sourceLists, resultList);
                    UpdateItemMembership(change.Current, sourceLists, resultList);
                    break;

                // Pass through refresh changes:
                case ListChangeReason.Refresh:
                    resultList.Refresh(change.Current);
                    break;

                // A move does not affect contents and so can be ignored:
                case ListChangeReason.Moved:
                    break;

                //// These should not occur as they are replaced by the Flatten operator:
                //// case ListChangeReason.AddRange:
                //// case ListChangeReason.RemoveRange:
                //// case ListChangeReason.Clear:

                default:
                    throw new ArgumentOutOfRangeException(nameof(change.Reason), "Unsupported change type");
            }
        }

        return resultList.CaptureChanges();
    }
}
