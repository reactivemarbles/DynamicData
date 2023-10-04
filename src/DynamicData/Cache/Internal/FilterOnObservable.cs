// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal class FilterOnObservable<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<bool>> _filterFactory;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;
    private readonly TimeSpan? _buffer;
    private readonly IScheduler _scheduler;

    public FilterOnObservable(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _filterFactory = filterFactory ?? throw new ArgumentNullException(nameof(filterFactory));
        _buffer = buffer;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var shared = _source.Transform(val => new FilterProxy(val)).Publish();

            // For each proxy object, create the filter observable, and use it to update the Filter property
            // Create a Refresh Change each time it is updated.
            // Merge the streams of Refresh Changes together.
            var changes =
                shared.MergeMany((proxy, key) =>
                        proxy.UpdateFilter(_filterFactory(proxy.Value, key))
                             .Select(_ => new Change<FilterProxy, TKey>(ChangeReason.Refresh, key, proxy)));

            // Create a stream of changesets from the stream of changes (either one at a time or buffered)
            IObservable<IChangeSet<FilterProxy, TKey>> refreshChanges = _buffer is null
                ? changes.Select(c => new ChangeSet<FilterProxy, TKey>(new[] { c }))
                : changes.Buffer(_buffer.Value, _scheduler)
                         .Where(list => list.Count > 0)
                         .Select(items => new ChangeSet<FilterProxy, TKey>(items));

            // Merge the Refresh Changes and the regular changes together
            // Filter for objects passing the filter
            // Transform back to the original object type
            var locker = new object();
            var publisher = shared.Synchronize(locker)
                                            .Merge(refreshChanges.Synchronize(locker))
                                            .Filter(proxy => proxy.PassesFilter)
                                            .Transform(proxy => proxy.Value)
                                            .SubscribeSafe(observer);

            return new CompositeDisposable(publisher, shared.Connect());
        });

    private class FilterProxy
    {
        public FilterProxy(TObject obj)
        {
            Value = obj;
        }

        public TObject Value { get; }

        public bool PassesFilter { get; private set; }

        public IObservable<bool> UpdateFilter(IObservable<bool> observable) =>
            observable.DistinctUntilChanged().Select(filterValue => PassesFilter = filterValue);
    }
}
