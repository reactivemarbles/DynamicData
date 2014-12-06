using System;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal class StaticFilter<TObject, TKey>
    {
        private readonly FilteredUpdater<TObject, TKey> _updater;
        private  readonly   Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();

        public StaticFilter(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        {
            _updater = new FilteredUpdater<TObject, TKey>(_cache, filter, parallelisationOptions);
        }

        public IChangeSet<TObject, TKey> Filter(IChangeSet<TObject, TKey> updates)
        {
              return _updater.Update(updates);
        }
    }
}