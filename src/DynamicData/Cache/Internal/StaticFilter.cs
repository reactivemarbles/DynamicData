// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class StaticFilter<TObject, TKey>
        where TKey : notnull
    {
        private readonly Func<TObject, bool> _filter;
        private readonly bool _suppressEmptyChangeSets;

        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        public StaticFilter(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, bool suppressEmptyChangeSets)
        {
            _source = source;
            _filter = filter;
            _suppressEmptyChangeSets = suppressEmptyChangeSets;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                ChangeAwareCache<TObject, TKey>? cache = null;

                return _source.Subscribe(changes =>
                {
                    cache ??= new ChangeAwareCache<TObject, TKey>(changes.Count);

                    cache.FilterChanges(changes, _filter);
                    var filtered = cache.CaptureChanges();

                    if (filtered.Count != 0 || !_suppressEmptyChangeSets)
                        observer.OnNext(filtered);

                }, observer.OnError, observer.OnCompleted);
            });
        }
    }
}