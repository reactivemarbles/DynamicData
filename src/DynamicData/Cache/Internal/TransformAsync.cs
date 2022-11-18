// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class TransformAsync<TDestination, TSource, TKey>
    where TKey : notnull
{
    private readonly Action<Error<TSource, TKey>>? _exceptionCallback;

    private readonly IObservable<Func<TSource, TKey, bool>>? _forceTransform;

    private readonly IObservable<IChangeSet<TSource, TKey>> _source;

    private readonly Func<TSource, Optional<TSource>, TKey, Task<TDestination>> _transformFactory;

    public TransformAsync(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>>? exceptionCallback, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
    {
        _source = source;
        _exceptionCallback = exceptionCallback;
        _transformFactory = transformFactory;
        _forceTransform = forceTransform;
    }

    public IObservable<IChangeSet<TDestination, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var cache = new ChangeAwareCache<TransformedItemContainer, TKey>();
            var asyncLock = new SemaphoreSlim(1, 1);

            var transformer = _source.SelectMany(async changes =>
            {
                await asyncLock.WaitAsync();
                return await DoTransform(cache, changes).ConfigureAwait(false);
            })
            .Do(_ => asyncLock.Release());

            if (_forceTransform is not null)
            {
                var locker = new object();
                var forced = _forceTransform.Synchronize(locker)
                .SelectMany(async shouldTransform =>
                {
                    await asyncLock.WaitAsync();
                    return await DoTransform(cache, shouldTransform).ConfigureAwait(false);
                })
                .Do(_ => asyncLock.Release());

                transformer = transformer.Synchronize(locker).Merge(forced);
            }

            return transformer.SubscribeSafe(observer);
        });
    }

    private async Task<IChangeSet<TDestination, TKey>> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, Func<TSource, TKey, bool> shouldTransform)
    {
        var toTransform = cache.KeyValues.Where(kvp => shouldTransform(kvp.Value.Source, kvp.Key)).Select(kvp => new Change<TSource, TKey>(ChangeReason.Update, kvp.Key, kvp.Value.Source, kvp.Value.Source)).ToArray();

        var transformed = await Task.WhenAll(toTransform.Select(Transform)).ConfigureAwait(false);

        return ProcessUpdates(cache, transformed);
    }

    private async Task<IChangeSet<TDestination, TKey>> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, IChangeSet<TSource, TKey> changes)
    {
        var transformed = await Task.WhenAll(changes.Select(Transform)).ConfigureAwait(false);

        return ProcessUpdates(cache, transformed);
    }

    private IChangeSet<TDestination, TKey> ProcessUpdates(ChangeAwareCache<TransformedItemContainer, TKey> cache, TransformResult[] transformedItems)
    {
        // check for errors and callback if a handler has been specified
        var errors = transformedItems.Where(t => !t.Success).ToArray();
        if (errors.Length > 0)
        {
            errors.ForEach(t => _exceptionCallback?.Invoke(new Error<TSource, TKey>(t.Error, t.Change.Current, t.Change.Key)));
        }

        foreach (var result in transformedItems.Where(t => t.Success))
        {
            TKey key = result.Key;
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
                    cache.Refresh(key);
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
            if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
            {
                var destination = await _transformFactory(change.Current, change.Previous, change.Key).ConfigureAwait(false);
                return new TransformResult(change, new TransformedItemContainer(change.Current, destination));
            }

            return new TransformResult(change);
        }
        catch (Exception ex)
        {
            // only handle errors if a handler has been specified
            if (_exceptionCallback is not null)
            {
                return new TransformResult(change, ex);
            }

            throw;
        }
    }

    private readonly struct TransformedItemContainer
    {
        public TransformedItemContainer(TSource source, TDestination destination)
        {
            Source = source;
            Destination = destination;
        }

        public TDestination Destination { get; }

        public TSource Source { get; }
    }

    private sealed class TransformResult
    {
        public TransformResult(Change<TSource, TKey> change, TransformedItemContainer container)
        {
            Change = change;
            Container = container;
            Success = true;
            Key = change.Key;
        }

        public TransformResult(Change<TSource, TKey> change)
        {
            Change = change;
            Container = Optional<TransformedItemContainer>.None;
            Success = true;
            Key = change.Key;
        }

        public TransformResult(Change<TSource, TKey> change, Exception error)
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
