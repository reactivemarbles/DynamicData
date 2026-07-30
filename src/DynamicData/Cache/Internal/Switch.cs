// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var synchronizationGate = InternalEx.NewLock();

                // What the current source has contributed, so that switching away can take it back out.
                var current = new Cache<TObject, TKey>();
                var subscription = new SerialDisposable();

                // Identifies the current source. A superseded one may still be mid-delivery, and anything
                // it produces after this point belongs to a source that has already been switched away from.
                var activeSourceId = 0;
                var isSourceRunning = false;
                var areSourcesComplete = false;

                var outer = _sources.SubscribeSafe(
                    source =>
                    {
                        int sourceId;

                        lock (synchronizationGate)
                        {
                            sourceId = ++activeSourceId;
                            isSourceRunning = true;

                            if (current.Count != 0)
                            {
                                observer.OnNext(new ChangeSet<TObject, TKey>(
                                    current.KeyValues.Select(static pair => new Change<TObject, TKey>(ChangeReason.Remove, pair.Key, pair.Value))));

                                current.Clear();
                            }
                        }

                        // Subscribed outside the lock. The source may deliver synchronously, and that
                        // delivery takes the lock for itself.
                        subscription.Disposable = source.SubscribeSafe(
                            changes =>
                            {
                                lock (synchronizationGate)
                                {
                                    if (sourceId != activeSourceId)
                                    {
                                        return;
                                    }

                                    current.Clone(changes);

                                    if (changes.Count != 0)
                                    {
                                        observer.OnNext(changes);
                                    }
                                }
                            },
                            error =>
                            {
                                lock (synchronizationGate)
                                {
                                    if (sourceId != activeSourceId)
                                    {
                                        return;
                                    }

                                    observer.OnError(error);
                                }
                            },
                            () =>
                            {
                                lock (synchronizationGate)
                                {
                                    if (sourceId != activeSourceId)
                                    {
                                        return;
                                    }

                                    isSourceRunning = false;

                                    if (areSourcesComplete)
                                    {
                                        observer.OnCompleted();
                                    }
                                }
                            });
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (synchronizationGate)
                        {
                            areSourcesComplete = true;

                            // The current source may still be running, and the result ends only once both have.
                            if (!isSourceRunning)
                            {
                                observer.OnCompleted();
                            }
                        }
                    });

                // Disposal order matters and CompositeDisposable does not specify one. The queue goes first
                // so that any delivery in flight is finished before the subscriptions feeding it are torn down.
                return Disposable.Create(() =>
                {
                    outer.Dispose();
                    subscription.Dispose();
                });
            });
}
