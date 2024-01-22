// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class TransformAsync<TDestination, TSource, TKey>(
    IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory,
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

            var transformer = source.Select(changes => DoTransform(cache, changes)).Concat();

            if (forceTransform is not null)
            {
                var locker = new object();
                var forced = forceTransform.Synchronize(locker)
                    .Select(shouldTransform => DoTransform(cache, shouldTransform)).Concat();

                transformer = transformer.Synchronize(locker).Merge(forced);
            }

            return transformer.SubscribeSafe(observer);
        });

    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, Func<TSource, TKey, bool> shouldTransform)
    {
        var toTransform = cache.KeyValues.Where(kvp => shouldTransform(kvp.Value.Source, kvp.Key)).Select(kvp =>
            new Change<TSource, TKey>(ChangeReason.Update, kvp.Key, kvp.Value.Source, kvp.Value.Source)).ToArray();

        return toTransform.Select(change => Observable.Defer(() => Transform(change).ToObservable()))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray()
            .Select(transformed => ProcessUpdates(cache, transformed));
    }

    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(
        ChangeAwareCache<TransformedItemContainer, TKey> cache, IChangeSet<TSource, TKey> changes)
    {
        return changes.Select(change => Observable.FromAsync(() => Transform(change)))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray()
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

    private async Task<TransformResult> Transform(Change<TSource, TKey> change)
    {
        try
        {
            if (change.Reason is ChangeReason.Add or ChangeReason.Update || (change.Reason is ChangeReason.Refresh && transformOnRefresh))
            {
                var destination = await transformFactory(change.Current, change.Previous, change.Key)
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
