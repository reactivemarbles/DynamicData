// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class StaticFilter<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, bool> _filter;

        public StaticFilter(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            _source = source;
            _filter = filter;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return _source.Scan((ChangeAwareCache<TObject, TKey>)null, (cache, changes) =>
                {
                    if (cache == null)
                    {
                        cache = new ChangeAwareCache<TObject, TKey>(changes.Count);
                    }

                    cache.FilterChanges(changes, _filter);
                    return cache;
                })
                .Select(cache => cache.CaptureChanges())
                .NotEmpty();
        }
    }
}
