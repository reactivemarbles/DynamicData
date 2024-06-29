// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class FilterImmutable<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Action<IObserver<IChangeSet<TObject, TKey>>, IChangeSet<TObject, TKey>> _onNextInvoker;
    private readonly Func<TObject, bool> _predicate;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public FilterImmutable(
        Func<TObject, bool> predicate,
        IObservable<IChangeSet<TObject, TKey>> source,
        bool suppressEmptyChangeSets)
    {
        _predicate = predicate;
        _source = source;

        // Optimize away the if check, if possible, per-instance
        _onNextInvoker = suppressEmptyChangeSets
            ? (observer, changes) =>
            {
                if (changes.Count is not 0)
                {
                    observer.OnNext(changes);
                }
            }
            : (observer, changes) => observer.OnNext(changes);
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
        => Observable.Create<IChangeSet<TObject, TKey>>(observer => _source
            .SubscribeSafe(Observer.Create<IChangeSet<TObject, TKey>>(
                onNext: upstreamChanges =>
                {
                    var downstreamChanges = new ChangeSet<TObject, TKey>(capacity: upstreamChanges.Count);

                    try
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            // Propagate changes as follows:
                            //      Don't propagate moves at all, since we don't preserve indexes.
                            //      For Updates, propagate...
                            //          ...an Add if the new item matches the predicate, but the old one doesn't
                            //          ...a Remove if the old item matches the predicate, but the new one doesn't
                            //          ...an Update if both items match the predicate
                            //          ...nothing if neither items match the predicate
                            //      For all other changes, propagate only if the value matches the predicate
                            if (change.Reason is ChangeReason.Moved)
                                continue;

                            ChangeReason? downstreamReason = change.Reason switch
                            {
                                ChangeReason.Update => (_predicate.Invoke(change.Previous.Value), _predicate.Invoke(change.Current)) switch
                                {
                                    (false, true) => ChangeReason.Add,
                                    (true, false) => ChangeReason.Remove,
                                    (true, true) => change.Reason,
                                    _ => null
                                },
                                _ => _predicate.Invoke(change.Current)
                                    ? change.Reason
                                    : null
                            };

                            if (downstreamReason is { } reason)
                            {
                                // Do not propagate indexes, we can't guarantee them to be correct, because we aren't caching items.
                                downstreamChanges.Add(new(
                                    reason: reason,
                                    key: change.Key,
                                    current: change.Current,
                                    previous: (reason is ChangeReason.Update)
                                        ? change.Previous
                                        : default));
                            }
                        }
                    }

                    // We're invoking "untrusted" consumer code, (I.E. _predicate), so catch and propagate any errors
                    catch (Exception error)
                    {
                        observer.OnError(error);
                    }

                    _onNextInvoker.Invoke(observer, downstreamChanges);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted)));
}
