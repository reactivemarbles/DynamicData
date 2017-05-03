using System;
using DynamicData.Annotations;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding
{
    /// <summary>
    /// Adaptor to relect a change set into an observable list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableCollectionAdaptor<T> : IChangeSetAdaptor<T>
    {
        private readonly IObservableCollection<T> _collection;
        private readonly int _refreshThreshold;
        private bool _loaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="collection">The collection.</param>
        /// <param name="refreshThreshold">The refresh threshold.</param>
        /// <exception cref="System.ArgumentNullException">collection</exception>
        public ObservableCollectionAdaptor([NotNull] IObservableCollection<T> collection, int refreshThreshold = 25)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _refreshThreshold = refreshThreshold;
        }

        /// <summary>
        /// Maintains the specified collection from the changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        public void Adapt(IChangeSet<T> changes)
        {
            if (changes.Count > _refreshThreshold || !_loaded)
            {
                using (_collection.SuspendNotifications())
                {
                    _collection.Clone(changes);
                    _loaded = true;
                }
            }
            else
            {
                _collection.Clone(changes);
            }
        }
    }

    /// <summary>
    /// Represents an adaptor which is used to update observable collection from
    /// a changeset stream
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class ObservableCollectionAdaptor<TObject, TKey> : IObservableCollectionAdaptor<TObject, TKey>
    {
        private readonly int _refreshThreshold;
        private bool _loaded;

        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public ObservableCollectionAdaptor(int refreshThreshold = 25)
        {
            _refreshThreshold = refreshThreshold;
        }

        /// <summary>
        /// Maintains the specified collection from the changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <param name="collection">The collection.</param>
        public void Adapt(IChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection)
        {
            _cache.Clone(changes);

            if (changes.Count > _refreshThreshold || !_loaded)
            {
                _loaded = true;
                using (collection.SuspendNotifications())
                {
                    collection.Load(_cache.Items);
                }
            }
            else
            {
                using (collection.SuspendCount())
                {
                    DoUpdate(changes, collection);
                }
            }
        }

        private void DoUpdate(IChangeSet<TObject, TKey> updates, IObservableCollection<TObject> list)
        {
            list.EnsureCapacityFor(updates);
            foreach (var update in updates)
            {
                switch (update.Reason)
                {
                    case ChangeReason.Add:
                        list.Add(update.Current);
                        break;

                    case ChangeReason.Remove:
                        list.Remove(update.Current);
                        break;
                    case ChangeReason.Update:
                        list.Remove(update.Previous.Value);
                        list.Add(update.Current);
                        break;
                }
            }
        }
    }
}
