using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class FilteredUpdater<TObject, TKey> : AbstractFilter<TObject, TKey>
    {
        public FilteredUpdater(ChangeAwareCache<TObject, TKey> cache, Func<TObject, bool> filter)
            : base(cache, filter)
        {
        }

        protected override IEnumerable<Change<TObject, TKey>> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory)
        {
            return items.Select(factory).SelectValues();
        }

        protected override IEnumerable<UpdateWithFilter> GetChangesWithFilter(IChangeSet<TObject, TKey> updates)
        {
            return updates.Select(u => new UpdateWithFilter(Filter(u.Current), u));
        }
    }
}
