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

        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        public StaticFilter(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            _source = source;
            _filter = filter;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return _source.Scan(
                (ChangeAwareCache<TObject, TKey>?)null,
                (cache, changes) =>
                    {
                        cache ??= new ChangeAwareCache<TObject, TKey>(changes.Count);

                        cache.FilterChanges(changes, _filter);
                        return cache;
                    })
                .Where(x => x is not null)
                .Select(x => x!)
                .Select(cache => cache.CaptureChanges()).NotEmpty();
        }
    }
}