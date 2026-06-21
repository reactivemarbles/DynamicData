// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformAsync class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformFactory">The transformFactory value.</param>
/// <param name="exceptionCallback">The exceptionCallback value.</param>
/// <param name="forceTransform">The forceTransform value.</param>
/// <param name="maximumConcurrency">The maximumConcurrency value.</param>
/// <param name="transformOnRefresh">The transformOnRefresh value.</param>
internal class TransformAsync<TDestination, TSource, TKey>(
    IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, Task<TDestination>> transformFactory,
    Action<Error<TSource, TKey>>? exceptionCallback,
    IObservable<Func<TSource, TKey, bool>>? forceTransform = null,
    int? maximumConcurrency = null,
    bool transformOnRefresh = false)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var cache = new ChangeAwareCache<TransformedItemContainer, TKey>();

            var transformer = source.Select(changes => DoTransform(cache, changes)).Concat();

            if (forceTransform is not null)
            {
                var queue = new SharedDeliveryQueue();
                var forced = forceTransform.SynchronizeSafe(queue)
                    .Select(shouldTransform => DoTransform(cache, shouldTransform)).Concat();

                transformer = transformer.SynchronizeSafe(queue).Merge(forced);

                return new CompositeDisposable(transformer.SubscribeSafe(observer), queue);
            }

            return transformer.SubscribeSafe(observer);
        });

    /// <summary>
    /// Executes the DoTransform operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="shouldTransform">The shouldTransform value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, Func<TSource, TKey, bool> shouldTransform)
    {
        var toTransform = cache.KeyValues.Where(kvp => shouldTransform(kvp.Value.Source, kvp.Key)).Select(kvp =>
            new Change<TSource, TKey>(ChangeReason.Update, kvp.Key, kvp.Value.Source, kvp.Value.Source)).ToArray();

        return toTransform.Select(change => Observable.Defer(() => Transform(change).ToObservable()))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray()
            .Select(transformed => ProcessUpdates(cache, transformed));
    }

    /// <summary>
    /// Executes the DoTransform operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="changes">The changes value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination, TKey>> DoTransform(
        ChangeAwareCache<TransformedItemContainer, TKey> cache, IChangeSet<TSource, TKey> changes)
    {
        return changes.Select(change => Observable.FromAsync(() => Transform(change)))
            .Merge(maximumConcurrency ?? int.MaxValue)
            .ToArray()
            .Select(transformed => ProcessUpdates(cache, transformed));
    }

    /// <summary>
    /// Executes the ProcessUpdates operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="transformedItems">The transformedItems value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the Transform operation.
    /// </summary>
    /// <param name="change">The change value.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents the TransformedItemContainer value.
/// </summary>
/// <param name="source">The source value.</param>
/// <param name="destination">The destination value.</param>
private readonly struct TransformedItemContainer(TSource source, TDestination destination)
    {
        /// <summary>
        /// Gets the Destination value.
        /// </summary>
        public TDestination Destination { get; } = destination;

        /// <summary>
        /// Gets the Source value.
        /// </summary>
        public TSource Source { get; } = source;
    }

/// <summary>
/// Provides members for the TransformResult class.
/// </summary>
private sealed class TransformResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransformResult"/> class.
        /// </summary>
        /// <param name="change">The change value.</param>
        /// <param name="container">The container value.</param>
        public TransformResult(in Change<TSource, TKey> change, in TransformedItemContainer container)
        {
            Change = change;
            Container = container;
            Success = true;
            Key = change.Key;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformResult"/> class.
        /// </summary>
        /// <param name="change">The change value.</param>
        public TransformResult(in Change<TSource, TKey> change)
        {
            Change = change;
            Container = ReactiveUI.Primitives.Optional<TransformedItemContainer>.None;
            Success = true;
            Key = change.Key;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformResult"/> class.
        /// </summary>
        /// <param name="change">The change value.</param>
        /// <param name="error">The error value.</param>
        public TransformResult(in Change<TSource, TKey> change, Exception error)
        {
            Change = change;
            Error = error;
            Success = false;
            Key = change.Key;
        }

        /// <summary>
        /// Gets the Change value.
        /// </summary>
        public Change<TSource, TKey> Change { get; }

        /// <summary>
        /// Gets the Container value.
        /// </summary>
        public ReactiveUI.Primitives.Optional<TransformedItemContainer> Container { get; }

        /// <summary>
        /// Gets the Error value.
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// Gets the Key value.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the Success value.
        /// </summary>
        public bool Success { get; }
    }
}
