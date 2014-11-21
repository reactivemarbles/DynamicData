using System.Linq;
using  DynamicData.Kernel;

namespace DynamicData.Binding
{
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
        private readonly CacheCloner<TObject, TKey> _cloner;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public ObservableCollectionAdaptor(int refreshThreshold = 25)
        {
            _refreshThreshold = refreshThreshold;
            _cloner = new CacheCloner<TObject, TKey>(_cache);
        }

        /// <summary>
        /// Maintains the specified collection from the changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <param name="collection">The collection.</param>
        public void Adapt(IChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection)
        {
            _cloner.Clone(changes);

            if (changes.Count > _refreshThreshold)
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
            updates.ForEach(update =>
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
                        {
                            list.Remove(update.Previous.Value);
                            list.Add(update.Current);
                        }
                        break;

                }
            });
        }

    }
}