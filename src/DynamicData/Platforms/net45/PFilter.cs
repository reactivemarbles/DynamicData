// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
using System.Reactive.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    internal sealed class PFilter<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        where TObject : notnull
        where TKey : notnull
    {
        public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var filterer = new PLinqFilteredUpdater(filter, parallelisationOptions);
                        return source.Select(filterer.Update).NotEmpty().SubscribeSafe(observer);
                    });

        private sealed class PLinqFilteredUpdater(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions) : AbstractFilter<TObject, TKey>(new ChangeAwareCache<TObject, TKey>(), filter)
        {
            private readonly ParallelisationOptions _parallelisationOptions = parallelisationOptions;

            protected override IEnumerable<UpdateWithFilter> GetChangesWithFilter(ChangeSet<TObject, TKey> updates)
            {
                if (updates.ShouldParallelise(_parallelisationOptions))
                {
                    return updates.Parallelise(_parallelisationOptions).Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
                }

                return updates.Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
            }

            protected override IEnumerable<Change<TObject, TKey>> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory)
            {
                var keyValuePairs = items as KeyValuePair<TKey, TObject>[] ?? items.ToArray();

                return keyValuePairs.ShouldParallelise(_parallelisationOptions) ? keyValuePairs.Parallelise(_parallelisationOptions).Select(factory).SelectValues() : keyValuePairs.Select(factory).SelectValues();
            }
        }
    }
}

#endif
