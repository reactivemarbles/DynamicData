using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class SizeLimiter<TObject, TKey>
    {
        private readonly Cache<ExpirableItem<TObject, TKey>, TKey> _cache = new Cache<ExpirableItem<TObject, TKey>, TKey>();
        private readonly IIntermediateUpdater<ExpirableItem<TObject, TKey>, TKey> _updater;

        private readonly int _sizeLimit;

        public SizeLimiter(int size)
        {
            _sizeLimit = size;
            _updater = new IntermediateUpdater<ExpirableItem<TObject, TKey>, TKey>(_cache);
        }

        public IChangeSet<TObject, TKey> Update(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
        {
            _updater.Update(updates);

            var itemstoexpire = _cache.KeyValues
                                      .OrderByDescending(exp => exp.Value.ExpireAt)
                                      .Skip(_sizeLimit)
                                      .Select(exp => new Change<TObject, TKey>(ChangeReason.Remove, exp.Key, exp.Value.Value))
                                      .ToList();

            if (itemstoexpire.Count > 0)
            {
                _updater.Remove(itemstoexpire.Select(exp => exp.Key));
            }

            var notifications = _updater.AsChangeSet();
            var changed = notifications.Select(update => new Change<TObject, TKey>
                                                   (
                                                   update.Reason,
                                                   update.Key,
                                                   update.Current.Value,
                                                   update.Previous.HasValue ? update.Previous.Value.Value : Optional<TObject>.None
                                                   ));

            return new ChangeSet<TObject, TKey>(changed);
        }

        public IChangeSet<TObject, TKey> CloneAndReturnExpiredOnly(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
        {
            _cache.Clone(updates);

            var itemstoexpire = _cache.KeyValues
                                      .OrderByDescending(exp => exp.Value.Index)
                                      .Skip(_sizeLimit)
                                      .Select(exp => new Change<TObject, TKey>(ChangeReason.Remove, exp.Key, exp.Value.Value))
                                      .ToList();

            if (itemstoexpire.Count > 0)
            {
                _updater.Remove(itemstoexpire.Select(exp => exp.Key));
            }

            var notifications = _updater.AsChangeSet();
            var changed = notifications.Select(update => new Change<TObject, TKey>
                                                   (
                                                   update.Reason,
                                                   update.Key,
                                                   update.Current.Value,
                                                   update.Previous.HasValue ? update.Previous.Value.Value : Optional<TObject>.None
                                                   ));

            return new ChangeSet<TObject, TKey>(changed);
        }
    }
}
