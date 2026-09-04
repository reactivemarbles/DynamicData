// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal class TransformAsync<TDestination, TSource, TKey>(
    IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, Optional<TSource>, TKey, CancellationToken, Task<TDestination>> transformFactory,
    Action<Error<TSource, TKey>>? exceptionCallback,
    IObservable<Func<TSource, TKey, bool>>? forceTransform = null,
    int? maximumConcurrency = null,
    bool transformOnRefresh = false)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var cache = new ChangeAwareCache<TransformedItemContainer, TKey>();

            if (forceTransform is null)
            {
                // A single Concat chain serializes itself: the next transform is not subscribed
                // until the previous one has completed, so nothing else can be touching the cache.
                return source.Select(changes => DoTransform(cache, changes)).Concat().SubscribeSafe(observer);
            }

            // Two independent Concat chains share the cache, and each applies its updates when its
            // async transforms finish, on whichever thread happens to complete them. So the cache
            // mutation, and the emission that immediately follows it, both have to run on the shared
            // queue. That serializes the two chains against each other, and it is also what
            // UnsynchronizedMerge requires of its inputs: without it the merge has no gate of its
            // own and the two chains can call the observer concurrently.
            var queue = new SharedDeliveryQueue();

            var transformer = source.Select(changes => DoTransform(cache, changes, queue)).Concat();

            // The forced predicate reads the cache to decide what to re-transform. That read runs
            // during this gated delivery, which keeps it exclusive with the updates applied by
            // either chain.
            var forced = forceTransform.SynchronizeSafe(queue)
                .Select(shouldTransform => DoTransform(cache, shouldTransform, queue)).Concat();

            return new CompositeDisposable(transformer.UnsynchronizedMerge(forced).SubscribeSafe(observer), queue);
        });

    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, Func<TSource, TKey, bool> shouldTransform, SharedDeliveryQueue queue)
    {
        var toTransform = cache.KeyValues.Where(kvp => shouldTransform(kvp.Value.Source, kvp.Key)).Select(kvp =>
            new Change<TSource, TKey>(ChangeReason.Update, kvp.Key, kvp.Value.Source, kvp.Value.Source)).ToArray();

        return toTransform.Select(change => Observable.FromAsync(t => Transform(change, t)))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray()
            .SynchronizeSafe(queue)
            .Select(transformed => ProcessUpdates(cache, transformed));
    }

    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(
        ChangeAwareCache<TransformedItemContainer, TKey> cache, IChangeSet<TSource, TKey> changes, SharedDeliveryQueue? queue = null)
    {
        var results = changes.Select(change => Observable.FromAsync(t => Transform(change, t)))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray();

        // Gated only when a forced-transform chain is also writing to the same cache.
        return (queue is null ? results : results.SynchronizeSafe(queue))
            .Select(transformed => ProcessUpdates(cache, transformed));
    }

    private ChangeSet<TDestination, TKey> ProcessUpdates(ChangeAwareCache<TransformedItemContainer, TKey> cache, TransformResult[] transformedItems)
    {
        // check for errors and callback if a handler has been specified
        var errors = transformedItems.Where(t => !t.Success).ToArray();
        if (errors.Length > 0)
        {
            errors.ForEach(t =>
                exceptionCallback?.Invoke(new Error<TSource, TKey>(t.Error, t.Change.Current, t.Change.Key)));
        }

        foreach (var result in transformedItems.Where(t => t.Success))
        {
            var key = result.Key;
            switch (result.Change.Reason)
            {
                case ChangeReason.Add:
                case ChangeReason.Update:
                    cache.AddOrUpdate(result.Container.Value, key);
                    break;

                case ChangeReason.Remove:
                    cache.Remove(key);
                    break;

                case ChangeReason.Refresh:
                    if (transformOnRefresh)
                    {
                        cache.AddOrUpdate(result.Container.Value, key);
                    }
                    else
                    {
                        cache.Refresh(key);
                    }

                    break;
            }
        }

        var changes = cache.CaptureChanges();

        var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, change.Current.Destination, change.Previous.Convert(x => x.Destination), change.CurrentIndex, change.PreviousIndex));

        return new ChangeSet<TDestination, TKey>(transformed);
    }

    private async Task<TransformResult> Transform(Change<TSource, TKey> change, CancellationToken cancellationToken)
    {
        try
        {
            if (change.Reason is ChangeReason.Add or ChangeReason.Update || (change.Reason is ChangeReason.Refresh && transformOnRefresh))
            {
                var destination = await transformFactory(change.Current, change.Previous, change.Key, cancellationToken)
                    .ConfigureAwait(false);
                return new TransformResult(change, new TransformedItemContainer(change.Current, destination));
            }

            return new TransformResult(change);
        }
        catch (Exception ex)
        {
            // only handle errors if a handler has been specified
            if (exceptionCallback is not null)
            {
                return new TransformResult(change, ex);
            }

            throw;
        }
    }

    private readonly struct TransformedItemContainer(TSource source, TDestination destination)
    {
        public TDestination Destination { get; } = destination;

        public TSource Source { get; } = source;
    }

    private sealed class TransformResult
    {
        public TransformResult(in Change<TSource, TKey> change, in TransformedItemContainer container)
        {
            Change = change;
            Container = container;
            Success = true;
            Key = change.Key;
        }

        public TransformResult(in Change<TSource, TKey> change)
        {
            Change = change;
            Container = Optional<TransformedItemContainer>.None;
            Success = true;
            Key = change.Key;
        }

        public TransformResult(in Change<TSource, TKey> change, Exception error)
        {
            Change = change;
            Error = error;
            Success = false;
            Key = change.Key;
        }

        public Change<TSource, TKey> Change { get; }

        public Optional<TransformedItemContainer> Container { get; }

        public Exception? Error { get; }

        public TKey Key { get; }

        public bool Success { get; }
    }
}
