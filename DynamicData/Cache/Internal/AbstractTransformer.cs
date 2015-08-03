using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Operators;

namespace DynamicData.Internal
{


    internal abstract class AbstractTransformer<TDestination, TSource, TKey>
    {
        private readonly object _locker = new object();
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly IIntermediateUpdater<TDestination, TKey> _updater;

        public AbstractTransformer(Action<Error<TSource, TKey>> exceptionCallback)
        {
            _updater = new IntermediateUpdater<TDestination, TKey>(new Cache<TDestination, TKey>());
            _exceptionCallback = exceptionCallback;
        }


        public IChangeSet<TDestination, TKey> Transform(IChangeSet<TSource, TKey> updates, Func<TSource, TDestination> transformFactory)
        {
            var notifications = DoTransform(updates, update =>
            {
                try
                {
                    if (update.Reason == ChangeReason.Add || update.Reason == ChangeReason.Update)
                    {
                        return new TransformedItem(update.Key, transformFactory(update.Current), update);
                    }

                    return new TransformedItem(update.Key, Optional.None<TDestination>(), update);
                }
                catch (Exception ex)
                {
                    //only handle errors if a handler has been specified
                    if (_exceptionCallback != null)
                    {
                        return new TransformedItem(ex, update);
                    }
                    throw;
                }
            });
            return notifications;
        }


        public IChangeSet<TDestination, TKey> Transform(IChangeSet<TSource, TKey> updates, Func<TSource, TKey, TDestination> transformFactory)
        {
            IChangeSet<TDestination, TKey> notifications;
            lock (_locker)
            {
                notifications = DoTransform(updates, update =>
                {
                    try
                    {
                        if (update.Reason == ChangeReason.Add || update.Reason == ChangeReason.Update)
                        {
                            return new TransformedItem(update.Key, transformFactory(update.Current, update.Key), update);
                        }
                        return new TransformedItem(update.Key, Optional.None<TDestination>(), update);
                    }
                    catch (Exception ex)
                    {
                        //only handle errors if a handler has been specified
                        if (_exceptionCallback != null)
                        {
                            return new TransformedItem(ex, update);
                        }
                        throw;
                    }
                });
            }
            return notifications;
        }


        protected abstract IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, TransformedItem> factory);

        protected IChangeSet<TDestination, TKey> ProcessUpdates(TransformedItem[] transformedItems)
        {
            //check for errors and callback if a handler has been specified
            var errors = transformedItems.Where(t => t.Error.HasValue).ToArray();
            if (errors.Any())
            {
                errors.ForEach(t => _exceptionCallback(new Error<TSource, TKey>(t.Error.Value, t.Change.Current, t.Change.Key)));
            }

            foreach (var update in transformedItems)
            {
                if (update.Error.HasValue)
                    continue;

                TKey key = update.Key.Value;
                switch (update.Change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                    {
                        _updater.AddOrUpdate(update.Transformed.Value, key);
                    }
                        break;

                    case ChangeReason.Remove:
                    {
                        _updater.Remove(key);
                    }
                        break;

                    case ChangeReason.Evaluate:
                    {
                        _updater.Evaluate(key);
                    }
                        break;
                }
            }

            return _updater.AsChangeSet();
        }


        protected struct TransformedItem
        {
            public TransformedItem(TKey key, Optional<TDestination> transformed, Change<TSource, TKey> change)
            {
                Key = key;
                Transformed = transformed;
                Change = change;
                Error = Optional.None<Exception>();
            }


            public TransformedItem(Exception exception, Change<TSource, TKey> change)
            {
                Key = Optional.None<TKey>();
                Transformed = Optional.None<TDestination>();
                Change = change;
                Error = exception;
            }

            public Optional<TDestination> Transformed { get; }

            public Optional<TKey> Key { get; }

            public Change<TSource, TKey> Change { get; }

            public Optional<Exception> Error { get; }
        }

    }
}