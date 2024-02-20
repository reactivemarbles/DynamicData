// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class AutoRefresh<TObject, TKey, TAny>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reEvaluator, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TAny>> _reEvaluator = reEvaluator ?? throw new ArgumentNullException(nameof(reEvaluator));

    private readonly IScheduler _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var shared = _source.Publish();

                // monitor each item observable and create change
                var changes = shared.MergeMany((t, k) => _reEvaluator(t, k).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, k, t)));

                // create a change set, either buffered or one item at the time
                IObservable<IChangeSet<TObject, TKey>> refreshChanges = buffer is null ?
                    changes.Select(c => new ChangeSet<TObject, TKey>(new[] { c })) :
                    changes.Buffer(buffer.Value, _scheduler).Where(list => list.Count > 0).Select(items => new ChangeSet<TObject, TKey>(items));

                // publish refreshes and underlying changes
                var locker = new object();
                var publisher = shared.Synchronize(locker).Merge(refreshChanges.Synchronize(locker)).SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
}
