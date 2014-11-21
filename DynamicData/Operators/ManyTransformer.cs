#region Usings

using System;
using System.Linq;
using DynamicData.Kernel;

#endregion<TSource,TKey>

namespace DynamicData.Operators
{
    internal sealed class ManyTransformer<TDestination, TSource, TKey>
    {
        private readonly Cache<TDestination, TKey> _cache = new Cache<TDestination, TKey>();
        private readonly object _locker = new object();
        private readonly Func<TSource, TDestination> _transformFactory;
        private readonly Action<IChangeSet<TDestination, TKey>> _updateAction;

        private readonly IIntermediateUpdater<TDestination, TKey> _updater;

        public ManyTransformer(Action<IChangeSet<TDestination, TKey>> updateAction,
                               Func<TSource, TDestination> transformFactory)
        {
            _updater = new IntermediateUpdater<TDestination, TKey>(_cache);
            _transformFactory = transformFactory;
            _updateAction = updateAction;
            _cache = new Cache<TDestination, TKey>();
        }

        #region ITransformObserver<TDestination,TSource> Members

        public void Transform(IChangeSet<TSource, TKey> updates)
        {
            IChangeSet<TDestination, TKey> notifications;
            lock (_locker)
            {
                notifications = DoTransform(new ChangeSet<TSource, TKey>(updates));
            }
            if (notifications.Count != 0)
            {
                _updateAction(notifications);
            }
        }

        #endregion

        private IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> changes)
        {
            IChangeSet<TDestination, TKey> notifications;
            lock (_locker)
            {

                //TODO: Get rid of the dictionary as it could throw (use proper tranformer)
                var transformed = changes
                    .AsParallel()
                    .AsOrdered()
                    .Where(update => update.Reason == ChangeReason.Add || update.Reason == ChangeReason.Update)
                    .Select(update => new
                        {
                            Current = _transformFactory(update.Current),
                            update.Key
                        })
                    .ToDictionary(update => update.Key);

                foreach (var update in changes)
                {
                    TKey key = update.Key;
                    switch (update.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                            {
                                _updater.AddOrUpdate(transformed[key].Current, key);
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
                notifications = _updater.AsChangeSet();
            }
            return notifications;
        }
    }
}