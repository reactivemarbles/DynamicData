using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Internal
{
    /// <summary>
    ///  Filters and maintains a cache
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal sealed class DynamicFilter<TObject, TKey> //: IFilterer<TObject,TKey>
    {
        private IFilter<TObject, TKey> _filteredUpdater;
        private readonly Cache<TObject, TKey> _all = new Cache<TObject, TKey>();
        private readonly Cache<TObject, TKey> _filtered = new Cache<TObject, TKey>();

        internal DynamicFilter()
        {
            _filteredUpdater = new FilteredUpdater<TObject, TKey>(_filtered, x => false);
        }

        public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
        {
            //maintain all so filter can be reapplied.
            _all.Clone(updates);
            return _filteredUpdater.Update(updates);
        }

        public IChangeSet<TObject, TKey> ApplyFilter(Func<TObject, bool> filter)
        {
            _filteredUpdater = new FilteredUpdater<TObject, TKey>(_filtered, filter);
            return Reevaluate(_all.KeyValues);
        }

        public IChangeSet<TObject, TKey> Evaluate(Func<TObject, bool> itemSelector)
        {
            return Reevaluate(_all.KeyValues.Where(t => itemSelector(t.Value)));
        }

        private IChangeSet<TObject, TKey> Reevaluate(IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            var result = _filteredUpdater.Evaluate(items);
            var changes = result.Where(u => u.Reason == ChangeReason.Add || u.Reason == ChangeReason.Remove);
            return new ChangeSet<TObject, TKey>(changes);
        }
    }
}
