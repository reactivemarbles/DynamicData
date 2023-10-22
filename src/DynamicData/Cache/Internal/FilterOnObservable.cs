﻿// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal class FilterOnObservable<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<bool>> _filterFactory;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;
    private readonly TimeSpan? _buffer;
    private readonly IScheduler? _scheduler;

    public FilterOnObservable(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _filterFactory = filterFactory ?? throw new ArgumentNullException(nameof(filterFactory));
        _buffer = buffer;
        _scheduler = scheduler;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        return _source.Transform((val, key) => new FilterProxy(val, _filterFactory(val, key)))
                      .AutoRefreshOnObservable(proxy => proxy.FilterObservable, _buffer, _scheduler)
                      .Filter(proxy => proxy.PassesFilter)
                      .Transform(proxy => proxy.Value);
    }

    private class FilterProxy
    {
        public FilterProxy(TObject obj, IObservable<bool> observable)
        {
            Value = obj;
            FilterObservable = observable.DistinctUntilChanged().Do(filterValue => PassesFilter = filterValue);
        }

        public IObservable<bool> FilterObservable { get; }

        public TObject Value { get; }

        public bool PassesFilter { get; private set; }
    }
}
