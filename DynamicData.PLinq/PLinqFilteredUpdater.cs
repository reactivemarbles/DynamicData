using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

namespace DynamicData.PLinq
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal class PLinqFilteredUpdater<TObject, TKey> : AbstractFilter<TObject, TKey>
    {
        private readonly ParallelisationOptions _parallelisationOptions;

        public PLinqFilteredUpdater(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
            : base(new ChangeAwareCache<TObject, TKey>(), filter)
        {
            _parallelisationOptions = parallelisationOptions;
        }

        public PLinqFilteredUpdater(ChangeAwareCache<TObject, TKey> cache, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
            : base(cache, filter)
        {
            _parallelisationOptions = parallelisationOptions;
        }

        protected override IEnumerable<Change<TObject, TKey>> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory)
        {
            var keyValuePairs = items as KeyValuePair<TKey, TObject>[] ?? items.ToArray();

            return keyValuePairs.ShouldParallelise(_parallelisationOptions)
                ? keyValuePairs.Parallelise(_parallelisationOptions).Select(factory).SelectValues()
                : keyValuePairs.Select(factory).SelectValues();
        }

        protected override IEnumerable<UpdateWithFilter> GetChangesWithFilter(IChangeSet<TObject, TKey> updates)
        {
            if (updates.ShouldParallelise(_parallelisationOptions))
            {
                return updates.Parallelise(_parallelisationOptions)
                              .Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
            }
            return updates.Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
        }
    }
}
