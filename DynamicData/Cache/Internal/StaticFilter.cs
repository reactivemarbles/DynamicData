using System;

namespace DynamicData.Internal
{
    internal class StaticFilter<TObject, TKey>
    {
        private readonly IFilter<TObject, TKey> _filter;

        public StaticFilter(IFilter<TObject, TKey> filter)
        {
            _filter = filter;
        }

        public StaticFilter(Func<TObject, bool> filter)
        {
            _filter = new FilteredUpdater<TObject, TKey>(new Cache<TObject, TKey>(), filter);
        }

        public IChangeSet<TObject, TKey> Filter(IChangeSet<TObject, TKey> updates)
        {
            return _filter.Update(updates);
        }
    }
}
