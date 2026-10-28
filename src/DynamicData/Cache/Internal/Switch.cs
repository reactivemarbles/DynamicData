// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
    {
        // Switching is done by hand rather than with Observable.Switch, which holds its gate across the
        // whole of downstream delivery. Here the gate is never held around user code, which can loop back
        // into this operator, so state read before such a call may be stale after it and every step
        // re-checks that its generation is still current.
        var queue = new DeliveryQueue<IChangeSet<TObject, TKey>>(observer);

        // What the current source has contributed, so that switching away can take it back out.
        var current = new Cache<TObject, TKey>();
        var currentSubscription = new SerialDisposable();

        // Doubles as the generation token, published before any user code runs so that a reentrant
        // selection can cancel a subscription whose Subscribe has not yet returned.
        SingleAssignmentDisposable? currentGeneration = null;

        // A selection made while an earlier Subscribe or Dispose frame is still on the stack cannot start
        // yet: that frame owns resources it cannot release until it unwinds, so the activation already in
        // progress owns the handoff loop and picks the latest selection up when it gets there.
        IObservable<IChangeSet<TObject, TKey>>? pendingSource = null;
        var isActivating = false;

        // Selected but not yet completed, so the result cannot complete while a source is still pending.
        var hasOutstandingSource = false;
        var areSourcesComplete = false;
        var isStopped = false;

        IDisposable sourcesSubscription;
        try
        {
            sourcesSubscription = _sources.SubscribeSafe(SelectSource, error => Fail(error, null), SourcesCompleted);
        }
        catch
        {
            queue.Dispose();
            currentSubscription.Dispose();

            throw;
        }

        return Disposable.Create(() =>
        {
            // Invalidate pending work before waiting for delivery; this scope does not drain.
            using (queue.AcquireReadLock())
            {
                isStopped = true;
                pendingSource = null;
            }

            // Finish downstream delivery before tearing down the sources feeding it.
            queue.Dispose();
            try
            {
                sourcesSubscription.Dispose();
            }
            finally
            {
                currentSubscription.Dispose();
            }
        });

        void SelectSource(IObservable<IChangeSet<TObject, TKey>> source)
        {
            SingleAssignmentDisposable generation;
            bool ownsHandoff;

            // Publish without draining: nothing may get ahead of releasing the previous source's resources.
            using (queue.AcquireReadLock())
            {
                if (isStopped || areSourcesComplete)
                {
                    return;
                }

                generation = new SingleAssignmentDisposable();
                currentGeneration = generation;
                hasOutstandingSource = true;

                // Latest selection always wins; only starting it can be deferred.
                pendingSource = source;
                ownsHandoff = !isActivating;
                isActivating = true;
            }

            // Disposing the outgoing subscription runs user teardown, so it happens outside the gate.
            currentSubscription.Disposable = generation;

            if (!ownsHandoff)
            {
                // An activation further down the stack starts this selection when it unwinds.
                return;
            }

            RunHandoffs();
        }

        // Starts selections one at a time, so a replacement never begins while a superseded frame unwinds.
        void RunHandoffs()
        {
            while (true)
            {
                IObservable<IChangeSet<TObject, TKey>>? source;
                SingleAssignmentDisposable? generation;

                using (queue.AcquireReadLock())
                {
                    source = pendingSource;
                    generation = currentGeneration;
                    pendingSource = null;

                    if (isStopped || source is null || generation is null)
                    {
                        isActivating = false;
                        return;
                    }
                }

                Activate(source, generation);
            }
        }

        void Activate(IObservable<IChangeSet<TObject, TKey>> source, SingleAssignmentDisposable generation)
        {
            using (var scope = queue.AcquireLock())
            {
                if (!IsCurrent(generation))
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

            // That scope delivered the clearing changeset, so a newer source may have been selected.
            // Skipping the subscribe is only an optimization; the handlers re-check anyway.
            using (queue.AcquireReadLock())
            {
                if (!IsCurrent(generation))
                {
                    return;
                }
            }

            // Subscribe outside the gate and assign only to this generation's holder: if a newer source was
            // selected synchronously, the holder is already disposed, so this assignment releases the late
            // subscription and the handoff loop then starts the newer source.
            generation.Disposable = source.SubscribeSafe(
                changes =>
                {
                    using var scope = queue.AcquireLock();

                    if (!IsCurrent(generation))
                    {
                        return;
                    }

                    current.Clone(changes);

                    if (changes.Count != 0)
                    {
                        scope.EnqueueNext(changes);
                    }
                },
                error => Fail(error, generation),
                () =>
                {
                    using var scope = queue.AcquireLock();

                    if (!IsCurrent(generation))
                    {
                        return;
                    }

                    hasOutstandingSource = false;

                    if (areSourcesComplete)
                    {
                        isStopped = true;
                        scope.EnqueueCompleted();
                    }
                });
        }

        void SourcesCompleted()
        {
            using var scope = queue.AcquireLock();

            if (isStopped)
            {
                return;
            }

            areSourcesComplete = true;

            // No source outstanding also covers the case where none was ever selected.
            if (!hasOutstandingSource)
            {
                isStopped = true;
                scope.EnqueueCompleted();
            }
        }

        void Fail(Exception error, SingleAssignmentDisposable? generation)
        {
            using var scope = queue.AcquireLock();

            if (isStopped || (generation is not null && !ReferenceEquals(generation, currentGeneration)))
            {
                return;
            }

            // Stop as soon as the terminal notification is queued, not only when it is delivered.
            isStopped = true;
            scope.EnqueueError(error);
        }

        // All callers hold the queue gate; notification handlers and handoffs use the same state.
        bool IsCurrent(SingleAssignmentDisposable generation) =>
            !isStopped && ReferenceEquals(generation, currentGeneration);
    });
}
