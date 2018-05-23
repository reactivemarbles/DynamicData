using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache
{
    public static class SourceCacheSuppressableExtensions
    {
        public static ISuppressableSourceCache<TObject, TKey> WithNotificationSuppressionSupport<TObject, TKey>(this SourceCache<TObject, TKey> self)
        {
            return new SuppressableSourceCache<TObject, TKey>(self);
        }
    }

    public interface ISuppressableSourceCache<TObject, TKey> : ISourceCache<TObject, TKey>
    {
        ISourceCache<TObject, TKey> SuppressNotifications();
    }

    internal class SuppressableSourceCache<TObject, TKey> : ISuppressableSourceCache<TObject, TKey>
    {
        private readonly ISourceCache<TObject, TKey> _target;
        private volatile bool _suppressed;

        internal SuppressableSourceCache(ISourceCache<TObject, TKey> target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _target.Watch(key).Where(_ => !_suppressed);
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            return _target.Connect().Where(_ => !_suppressed);
        }

        public IObservable<int> CountChanged
        {
            get { return _target.CountChanged.Where(_ => !_suppressed); }
        }

        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            _target.Edit(updateAction);
        }

        public void Dispose()
        {
            if (_suppressed)
            {
                _suppressed = false;
                return;
            }

            _target.Dispose();
        }

        public IEnumerable<TKey> Keys => _target.Keys;

        public IEnumerable<TObject> Items => _target.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _target.KeyValues;

        public Optional<TObject> Lookup(TKey key)
        {
            return _target.Lookup(key);
        }

        public int Count => _target.Count;

        public void OnCompleted()
        {
            _target.OnCompleted();
        }

        public void OnError(Exception exception)
        {
            _target.OnError(exception);
        }

        public ISourceCache<TObject, TKey> SuppressNotifications()
        {
            _suppressed = true;
            return this;
        }
    }
}