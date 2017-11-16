#if P_LINQ

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    internal class PFilter<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, bool> _filter;
        private readonly ParallelisationOptions _parallelisationOptions;

        public PFilter(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        {
            _source = source;
            _filter = filter;
            _parallelisationOptions = parallelisationOptions;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var filterer = new PLinqFilteredUpdater(_filter, _parallelisationOptions);
                return _source
                    .Select(filterer.Update)
                    .NotEmpty()
                    .SubscribeSafe(observer);
            });
        }

        private  class PLinqFilteredUpdater: AbstractFilter<TObject, TKey>
        {
            private readonly ParallelisationOptions _parallelisationOptions;

            public PLinqFilteredUpdater(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
                : base(new ChangeAwareCache<TObject, TKey>(), filter)
            {
                _parallelisationOptions = parallelisationOptions;
            }

            protected override IEnumerable<Change<TObject, TKey>> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory)
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




}
#endif
