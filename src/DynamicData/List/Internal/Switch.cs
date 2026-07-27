// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class Switch<T>(IObservable<IObservable<IChangeSet<T>>> sources)
    where T : notnull
{
    private readonly IObservable<IObservable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                // Switching is done by hand rather than with Observable.Switch, which holds its gate for
                // the whole of downstream delivery. The queue enqueues and returns instead, so a producer
                // is never held up by whatever a subscriber does with the notification, and a pipeline
                // crossing into another collection cannot deadlock against it.
                var queue = new DeliveryQueue<IChangeSet<T>>(observer);

                // What the current source has contributed, so that switching away can take it back out.
                var current = new List<T>();
                var subscription = new SerialDisposable();

                // Identifies the current source. A superseded one may still be mid-delivery, and anything
                // it produces after this point belongs to a source that has already been switched away from.
                var active = 0;
                var isSourceRunning = false;
                var areSourcesComplete = false;

                var outer = _sources.Subscribe(
                    source =>
                    {
                        int id;

                        using (var scope = queue.AcquireLock())
                        {
                            id = ++active;
                            isSourceRunning = true;

                            if (current.Count != 0)
                            {
                                scope.EnqueueNext(new ChangeSet<T> { new Change<T>(ListChangeReason.Clear, current.ToArray()) });
                                current.Clear();
                            }
                        }

                        // Subscribed outside the lock. The source may deliver synchronously, and that
                        // delivery takes the lock for itself.
                        subscription.Disposable = source.Subscribe(
                            changes =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                current.Clone(changes);

                                if (changes.Count != 0)
                                {
                                    scope.EnqueueNext(changes);
                                }
                            },
                            error =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                scope.EnqueueError(error);
                            },
                            () =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                isSourceRunning = false;

                                if (areSourcesComplete)
                                {
                                    scope.EnqueueCompleted();
                                }
                            });
                    },
                    queue.OnError,
                    () =>
                    {
                        using var scope = queue.AcquireLock();

                        areSourcesComplete = true;

                        // The current source may still be running, and the result ends only once both have.
                        if (!isSourceRunning)
                        {
                            scope.EnqueueCompleted();
                        }
                    });

                // Queue first, so that delivery is finished before the subscriptions are torn down.
                return new CompositeDisposable(queue, outer, subscription);
            });
}
