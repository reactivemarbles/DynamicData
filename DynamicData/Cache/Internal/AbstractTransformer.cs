using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal abstract class AbstractTransformer<TDestination, TSource, TKey>
    {
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly ChangeAwareCache<TransformedItemContainer, TKey> _innerCache = new ChangeAwareCache<TransformedItemContainer, TKey>();

        public AbstractTransformer(Action<Error<TSource, TKey>> exceptionCallback)
        {
            _exceptionCallback = exceptionCallback;
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

        protected abstract IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, Optional<TransformResult>> factory);

        protected abstract IChangeSet<TDestination, TKey> DoTransform(IEnumerable<KeyValuePair<TKey, TSource>> items, Func<KeyValuePair<TKey, TSource>, TransformResult> factory);

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

                    case ChangeReason.Evaluate:
                        _innerCache.Evaluate(key);
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


    internal class Transform<TDestination, TSource, TKey>
    {
        private readonly IObservable<IChangeSet<TSource, TKey>> _source;
        private readonly Func<TSource, Optional<TSource>, TKey, TDestination> _transformFactory;
        private readonly IObservable<Func<TSource, TKey, bool>> _forceTransform;
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly int _maximumConcurrency;

        public Transform(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> exceptionCallback = null, int maximumConcurrency = 1, IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            _source = source;
            _exceptionCallback = exceptionCallback;
            _transformFactory = transformFactory;
            _maximumConcurrency = maximumConcurrency;
            _forceTransform = forceTransform;
        }

        public IObservable<IChangeSet<TDestination, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
            {
                var cache = new ChangeAwareCache<TransformedItemContainer, TKey>();
                var transformer = _source.Select(changes => DoTransform(cache, changes));

                if (_forceTransform != null)
                {
                    var locker = new object();
                    var forced = _forceTransform
                        .Synchronize(locker)
                        .Select(shouldTransform => DoTransform(cache, shouldTransform));

                    transformer = transformer.Synchronize(locker).Merge(forced);
                }

                return transformer.SubscribeSafe(observer);
            });
        }

        private  IChangeSet<TDestination, TKey> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, Func<TSource, TKey, bool> shouldTransform)
        {
            var toTransform = cache.KeyValues
                          .Where(kvp => shouldTransform(kvp.Value.Source, kvp.Key))
                          .Select(kvp => new Change<TSource, TKey>(ChangeReason.Update, kvp.Key, kvp.Value.Source, kvp.Value.Source))
                          .ToArray();

            var transformed =  toTransform.Select(c => Select(cache, c));
            return ProcessUpdates(cache, transformed.ToArray());
        }

        private  IChangeSet<TDestination, TKey> DoTransform(ChangeAwareCache<TransformedItemContainer, TKey> cache, IChangeSet<TSource, TKey> changes)
        {
            var transformed =  changes.Select(c => Select(cache, c));
            return ProcessUpdates(cache, transformed.ToArray());
        }

        private  TransformResult Select(ChangeAwareCache<TransformedItemContainer, TKey> target, Change<TSource, TKey> change)
        {
            try
            {
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                {
                    var destination = _transformFactory(change.Current, change.Previous, change.Key);
                    return new TransformResult(change, new TransformedItemContainer(change.Key, change.Current, destination));
                }

                var existing = target.Lookup(change.Key)
                    .ValueOrThrow(() => CreateMissingKeyException(change.Reason, change.Key));

                return new TransformResult(change, existing);
            }
            catch (Exception ex)
            {
                //only handle errors if a handler has been specified
                if (_exceptionCallback != null)
                    return new TransformResult(change, ex);
                throw;
            }
        }

        private Exception CreateMissingKeyException(ChangeReason reason, TKey key)
        {
            var message = $"{key} is missing. The change reason is '{reason}'." +
                          $"{Environment.NewLine}Object type {typeof(TSource)}, Key type {typeof(TKey)}, Destination type is {typeof(TDestination)}";
            return new MissingKeyException(message);
        }

        private IChangeSet<TDestination, TKey> ProcessUpdates(ChangeAwareCache<TransformedItemContainer, TKey> cache, TransformResult[] transformedItems)
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
                        cache.AddOrUpdate(result.Container, key);
                        break;

                    case ChangeReason.Remove:
                        cache.Remove(key);
                        break;

                    case ChangeReason.Evaluate:
                        cache.Evaluate(key);
                        break;
                }
            }

            var changes = cache.CaptureChanges();
            var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                                                                                      change.Key,
                                                                                      change.Current.Destination,
                                                                                      change.Previous.Convert(x => x.Destination),
                                                                                      change.CurrentIndex,
                                                                                      change.PreviousIndex));

            return new ChangeSet<TDestination, TKey>(transformed);
        }

        private sealed class TransformResult
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
    }
}
