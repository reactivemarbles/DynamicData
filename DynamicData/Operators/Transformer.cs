#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

#endregion<TSource,TKey>

namespace DynamicData.Operators
{
    internal class Transformer<TDestination, TSource, TKey>
    {
        private readonly object _locker = new object();
        private readonly ParallelisationOptions _parallelisationOptions;
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly IIntermediateUpdater<TDestination, TKey> _updater;

        public Transformer(ParallelisationOptions parallelisationOptions, Action<Error<TSource, TKey>> exceptionCallback)
        {
            _updater = new IntermediateUpdater<TDestination, TKey>(new Cache<TDestination, TKey>());
            _parallelisationOptions = parallelisationOptions;
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


        public IChangeSet<TDestination, TKey> Transform(IChangeSet<TSource, TKey> updates, Func<TSource,TKey, TDestination> transformFactory)
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
                                return new TransformedItem(update.Key, transformFactory(update.Current, update.Key),update);
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

        private IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, TransformedItem> factory)
        {
            //do transform first.
            var transformed = updates.ShouldParallelise(_parallelisationOptions) 
                            ? updates.Parallelise(_parallelisationOptions).Select(factory).ToArray() 
                            : updates.Select(factory).ToArray();

            return ProcessUpdates( transformed);
        }

        private IChangeSet<TDestination, TKey> ProcessUpdates(TransformedItem[] transformedItems)
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


        private struct TransformedItem
        {
            private readonly Optional<Exception> _error;
            private readonly Optional<TKey> _key;
            private readonly Optional<TDestination> _destination;

            private readonly Change<TSource,TKey> _change;

            public TransformedItem(TKey key, Optional<TDestination> transformed, Change<TSource, TKey> change)
            {
                _key = key;
                _destination = transformed;
                _change = change;
                _error = Optional.None<Exception>();
            }


            public TransformedItem(Exception exception, Change<TSource, TKey> change)
            {
                _key = Optional.None<TKey>();
                _destination = Optional.None<TDestination>();
                _change = change;
                _error = exception;
            }

            public Optional<TDestination> Transformed
            {
                get { return _destination; }
            }

            public Optional<TKey> Key
            {
                get { return _key; }
            }

            public Change<TSource, TKey> Change
            {
                get { return _change; }
            }

            public Optional<Exception> Error
            {
                get { return _error; }
            }
        }

    }
}