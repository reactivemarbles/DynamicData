using System;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal class DefaultFilterer<TObject, TKey>
    {
        private readonly FilteredUpdater<TObject, TKey> _updater;
        private  readonly   Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();

        public DefaultFilterer(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        {
            _updater = new FilteredUpdater<TObject, TKey>(_cache, filter, parallelisationOptions);
        }

        #region IFilterer<T> Members

        public IChangeSet<TObject, TKey> Filter(IChangeSet<TObject, TKey> updates)
        {
              return _updater.Update(updates);
        }

        #endregion
    }
}