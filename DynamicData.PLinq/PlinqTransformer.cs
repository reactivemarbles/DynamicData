using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.PLinq
{
    internal class PlinqTransformer<TDestination, TSource, TKey>
    {
        private readonly ParallelisationOptions _parallelisationOptions;
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly ChangeAwareCache<TransformedItemContainer, TKey> _innerCache = new ChangeAwareCache<TransformedItemContainer, TKey>();

        public PlinqTransformer(ParallelisationOptions parallelisationOptions, Action<Error<TSource, TKey>> exceptionCallback)
        {
            _parallelisationOptions = parallelisationOptions;
            _exceptionCallback = exceptionCallback;
        }

        protected IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, Optional<TransformResult>> factory)
        {
            var transformed = updates.ShouldParallelise(_parallelisationOptions)
                ? updates.Parallelise(_parallelisationOptions).Select(factory).SelectValues().ToArray()
                : updates.Select(factory).SelectValues().ToArray();

            return ProcessUpdates(transformed);
        }

        protected IChangeSet<TDestination, TKey> DoTransform(IEnumerable<KeyValuePair<TKey, TSource>> items, Func<KeyValuePair<TKey, TSource>, TransformResult> factory)
        {
            var keyValuePairs = items.AsArray();

            var transformed = keyValuePairs.ShouldParallelise(_parallelisationOptions)
                ? keyValuePairs.Parallelise(_parallelisationOptions).Select(factory).ToArray()
                : keyValuePairs.Select(factory).ToArray();

            return ProcessUpdates(transformed);
        }


        public IChangeSet<TDestination, TKey> Transform(IChangeSet<TSource, TKey> updates, Func<TSource, TDestination> transformFactory)
        {
            return DoTransform(updates, update => Transform(update, change => transformFactory(change.Current)));
        }

        public IChangeSet<TDestination, TKey> Transform(IChangeSet<TSource, TKey> updates, Func<TSource, TKey, TDestination> transformFactory)
        {
            return DoTransform(updates, update => Transform(update, change => transformFactory(change.Current, change.Key)));
        }

        public IChangeSet<TDestination, TKey> ForceTransform(Func<TSource, bool> shouldForce, Func<TSource, TDestination> transformFactory)
        {
            var toTransform = _innerCache.KeyValues
                                      .Select(x => new KeyValuePair<TKey, TSource>(x.Key, x.Value.Source))
                                      .Where(kvp => shouldForce(kvp.Value))
                                      .ToArray();

            return DoTransform(toTransform, kvp => Transform(kvp, x => transformFactory(x.Value)));
        }

        public IChangeSet<TDestination, TKey> ForceTransform(Func<TSource, TKey, bool> shouldForce, Func<TSource, TKey, TDestination> transformFactory)
        {
            var toTransform = _innerCache.KeyValues
                                      .Select(x => new KeyValuePair<TKey, TSource>(x.Key, x.Value.Source))
                                      .Where(kvp => shouldForce(kvp.Value, kvp.Key))
                                      .ToArray();

            return DoTransform(toTransform, kvp => Transform(kvp, x => transformFactory(x.Value, x.Key)));
        }

        private Optional<TransformResult> Transform(Change<TSource, TKey> change, Func<Change<TSource, TKey>, TDestination> transformFactory)
        {
            try
            {
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                {
                    var destination = transformFactory(change);
                    return new TransformResult(change, new TransformedItemContainer(change.Key, change.Current, destination));
                }

                var existing = _innerCache.Lookup(change.Key);
                if (!existing.HasValue)
                    return Optional.None<TransformResult>();
                return new TransformResult(change, existing.Value);
            }
            catch (Exception ex)
            {
                //only handle errors if a handler has been specified
                if (_exceptionCallback != null)
                    return new TransformResult(change, ex);

                throw;
            }
        }

        private TransformResult Transform(KeyValuePair<TKey, TSource> kvp, Func<KeyValuePair<TKey, TSource>, TDestination> transformFactory)
        {
            //let's assume that there will always be an original when we force a transform!
            var original = _innerCache.Lookup(kvp.Key);
            var change = new Change<TSource, TKey>(ChangeReason.Add, kvp.Key, original.Value.Source);

            try
            {
                var transformed = transformFactory(kvp);
                var container = new TransformedItemContainer(kvp.Key, kvp.Value, transformed);
                return new TransformResult(change, container);
            }
            catch (Exception ex)
            {
                //only handle errors if a handler has been specified
                if (_exceptionCallback != null)
                    return new TransformResult(change, ex);

                throw;
            }
        }


        protected IChangeSet<TDestination, TKey> ProcessUpdates(TransformResult[] transformedItems)
        {
            //check for errors and callback if a handler has been specified
            var errors = transformedItems.Where(t => !t.Success).ToArray();
            if (errors.Any())
                errors.ForEach(t => _exceptionCallback(new Error<TSource, TKey>(t.Error, t.Change.Current, t.Change.Key)));

            foreach (var result in transformedItems)
            {
                if (!result.Success)
                    continue;

                TKey key = result.Container.Key;
                switch (result.Change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                        _innerCache.AddOrUpdate(result.Container, key);
                        break;

                    case ChangeReason.Remove:
                        _innerCache.Remove(key);
                        break;

                    case ChangeReason.Refresh:
                        _innerCache.Refresh(key);
                        break;
                }
            }

            var changes = _innerCache.CaptureChanges();
            var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                                                                                      change.Key,
                                                                                      change.Current.Destination,
                                                                                      change.Previous.Convert(x => x.Destination),
                                                                                      change.CurrentIndex,
                                                                                      change.PreviousIndex));

            return new ChangeSet<TDestination, TKey>(transformed);
        }

        protected sealed class TransformedItemContainer
        {
            public TKey Key { get; }
            public TSource Source { get; }
            public TDestination Destination { get; }

            public TransformedItemContainer(TKey key, TSource source, TDestination destination)
            {
                Key = key;
                Source = source;
                Destination = destination;
            }
        }

        protected sealed class TransformResult
        {
            public Change<TSource, TKey> Change { get; }
            public Exception Error { get; }
            public bool Success { get; }

            public TransformedItemContainer Container { get; }

            public TransformResult(Change<TSource, TKey> change, TransformedItemContainer container)
            {
                Change = change;
                Container = container;
                Success = true;
            }

            public TransformResult(Change<TSource, TKey> change, Exception error)
            {
                Change = change;
                Error = error;
                Success = false;
            }
        }
    }
}

