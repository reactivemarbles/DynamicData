// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Using(
            static () => new SingleAssignmentDisposable(),
            lifetime => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                // Switching is done by hand rather than with Observable.Switch, which holds its gate for
                // the whole of downstream delivery. The queue enqueues and returns instead, so a producer
                // is never held up by whatever a subscriber does with the notification, and a pipeline
                // crossing into another cache cannot deadlock against it.
                var queue = new DeliveryQueue<IChangeSet<TObject, TKey>>(observer);

                // What the current source has contributed, so that switching away can take it back out.
                var current = new Cache<TObject, TKey>();
                var outer = new SingleAssignmentDisposable();

                // The holder is also the generation token. Publish it before calling any user code, so
                // reentrant selection can cancel a subscription whose Subscribe has not yet returned.
                SingleAssignmentDisposable? activeSubscription = null;
                var isSourceRunning = false;
                var areSourcesComplete = false;
                var isStopped = false;

                // Using owns this slot before activation, including synchronous subscription failures.
                lifetime.Disposable = Disposable.Create(() =>
                {
                    SingleAssignmentDisposable? subscription;

                    // This scope does not drain. Invalidate pending work before waiting for delivery,
                    // and never run subscription teardown under the queue's gate.
                    using (queue.AcquireReadLock())
                    {
                        isStopped = true;
                        subscription = activeSubscription;
                        activeSubscription = null;
                    }

                    // Finish downstream delivery before tearing down the sources feeding it.
                    queue.Dispose();
                    try
                    {
                        outer.Dispose();
                    }
                    finally
                    {
                        subscription?.Dispose();
                    }
                });

                outer.Disposable = _sources.SubscribeSafe(
                    SwitchSource,
                    error => Fail(error, null),
                    () =>
                    {
                        using var scope = queue.AcquireLock();

                        if (isStopped)
                        {
                            return;
                        }

                        areSourcesComplete = true;

                        // A selected source counts as running even while its subscription is pending.
                        if (!isSourceRunning)
                        {
                            isStopped = true;
                            scope.EnqueueCompleted();
                        }
                    });

                return Disposable.Empty;

                void SwitchSource(IObservable<IChangeSet<TObject, TKey>> source)
                {
                    SingleAssignmentDisposable subscription;
                    SingleAssignmentDisposable? previous;

                    // Publish without draining notifications: even a reset callback must not get ahead
                    // of releasing the previous source's resources.
                    using (queue.AcquireReadLock())
                    {
                        if (isStopped || areSourcesComplete)
                        {
                            return;
                        }

                        subscription = new SingleAssignmentDisposable();
                        previous = activeSubscription;
                        activeSubscription = subscription;
                        isSourceRunning = true;
                    }

                    // Disposal is user code too: it can select another source or terminate the result.
                    previous?.Dispose();

                    using (var scope = queue.AcquireLock())
                    {
                        if (!IsCurrent(subscription))
                        {
                            return;
                        }

                        if (current.Count != 0)
                        {
                            scope.EnqueueNext(new ChangeSet<TObject, TKey>(
                                current.KeyValues.Select(static pair => new Change<TObject, TKey>(ChangeReason.Remove, pair.Key, pair.Value))));

                            current.Clear();
                        }
                    }

                    // Reset delivery is another reentrant boundary. Outer completion alone does not
                    // cancel this source, but disposal, failure, or a newer selection does.
                    using (queue.AcquireReadLock())
                    {
                        if (!IsCurrent(subscription))
                        {
                            return;
                        }
                    }

                    // Subscribe outside the gate and assign only to this generation's holder. If a
                    // synchronous notification selected a newer source, this holder is already disposed
                    // and disposes the late-returning subscription without touching the newer one.
                    subscription.Disposable = source.SubscribeSafe(
                        changes =>
                        {
                            using var scope = queue.AcquireLock();

                            if (!IsCurrent(subscription))
                            {
                                return;
                            }

                            current.Clone(changes);

                            if (changes.Count != 0)
                            {
                                scope.EnqueueNext(changes);
                            }
                        },
                        error => Fail(error, subscription),
                        () =>
                        {
                            using var scope = queue.AcquireLock();

                            if (!IsCurrent(subscription))
                            {
                                return;
                            }

                            isSourceRunning = false;

                            if (areSourcesComplete)
                            {
                                isStopped = true;
                                scope.EnqueueCompleted();
                            }
                        });
                }

                void Fail(Exception error, SingleAssignmentDisposable? subscription)
                {
                    using var scope = queue.AcquireLock();

                    if (isStopped || (subscription is not null && !ReferenceEquals(subscription, activeSubscription)))
                    {
                        return;
                    }

                    // Stop as soon as the terminal notification is queued, not only when it is delivered.
                    isStopped = true;
                    scope.EnqueueError(error);
                }

                // All callers hold the queue gate; notification handlers and handoffs use the same state.
                bool IsCurrent(SingleAssignmentDisposable subscription) =>
                    !isStopped && ReferenceEquals(subscription, activeSubscription);
            }));
}
