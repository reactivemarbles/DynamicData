// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class ToObservableChangeSet<TObject, TKey>
        where TKey : notnull
    {
        private readonly Func<TObject, TimeSpan?>? _expireAfter;

        private readonly Func<TObject, TKey> _keySelector;

        private readonly int _limitSizeTo;

        private readonly IScheduler _scheduler;

        private readonly bool _singleValueSource;

        private readonly IObservable<IEnumerable<TObject>> _source;

        public ToObservableChangeSet(IObservable<TObject> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null)
            : this(source.Select(t => new[] { t }), keySelector, expireAfter, limitSizeTo, scheduler, true)
        {
        }

        public ToObservableChangeSet(IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, Func<TObject, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null, bool singleValueSource = false)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _expireAfter = expireAfter;
            _limitSizeTo = limitSizeTo;
            _scheduler = scheduler ?? Scheduler.Default;
            _singleValueSource = singleValueSource;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        long orderItemWasAdded = -1;
                        var locker = new object();

                        if (_expireAfter is null && _limitSizeTo < 1)
                        {
                            return _source.Scan(
                                new ChangeAwareCache<TObject, TKey>(),
                                (state, latest) =>
                                    {
                                        if (latest is IList<TObject> list)
                                        {
                                            // zero allocation enumerator
                                            var enumerableList = EnumerableIList.Create(list);
                                            if (!_singleValueSource)
                                            {
                                                state.Remove(state.Keys.Except(enumerableList.Select(_keySelector)).ToList());
                                            }

                                            foreach (var item in enumerableList)
                                            {
                                                state.AddOrUpdate(item, _keySelector(item));
                                            }
                                        }
                                        else
                                        {
                                            var enumerable = latest.ToList();
                                            if (!_singleValueSource)
                                            {
                                                state.Remove(state.Keys.Except(enumerable.Select(_keySelector)).ToList());
                                            }

                                            foreach (var item in enumerable)
                                            {
                                                state.AddOrUpdate(item, _keySelector(item));
                                            }
                                        }

                                        return state;
                                    }).Select(state => state.CaptureChanges()).SubscribeSafe(observer);
                        }

                        var cache = new ChangeAwareCache<ExpirableItem<TObject, TKey>, TKey>();
                        var sizeLimited = _source.Synchronize(locker).Scan(
                            cache,
                            (state, latest) =>
                                {
                                    latest.Select(
                                        t =>
                                            {
                                                var key = _keySelector(t);
                                                return CreateExpirableItem(t, key, ref orderItemWasAdded);
                                            }).ForEach(ei => cache.AddOrUpdate(ei, ei.Key));

                                    if (_limitSizeTo > 0 && state.Count > _limitSizeTo)
                                    {
                                        var toRemove = state.Count - _limitSizeTo;

                                        // remove oldest items
                                        cache.KeyValues.OrderBy(exp => exp.Value.Index).Take(toRemove).ForEach(ei => cache.Remove(ei.Key));
                                    }

                                    return state;
                                }).Select(state => state.CaptureChanges()).Publish();

                        var timeLimited = (_expireAfter is null ? Observable.Never<IChangeSet<ExpirableItem<TObject, TKey>, TKey>>() : sizeLimited).Filter(ei => ei.ExpireAt != DateTime.MaxValue).MergeMany(
                            grouping =>
                                {
                                    var expireAt = grouping.ExpireAt.Subtract(_scheduler.Now.DateTime);
                                    return Observable.Timer(expireAt, _scheduler).Select(_ => grouping);
                                }).Synchronize(locker).Select(
                            item =>
                                {
                                    cache.Remove(item.Key);
                                    return cache.CaptureChanges();
                                });

                        var publisher = sizeLimited.Merge(timeLimited).Cast(ei => ei.Value).NotEmpty().SubscribeSafe(observer);

                        return new CompositeDisposable(publisher, sizeLimited.Connect());
                    });
        }

        private ExpirableItem<TObject, TKey> CreateExpirableItem(TObject item, TKey key, ref long orderItemWasAdded)
        {
            // check whether expiry has been set for any items
            var dateTime = _scheduler.Now.DateTime;
            var removeAt = _expireAfter?.Invoke(item);
            var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;

            return new ExpirableItem<TObject, TKey>(item, key, expireAt, Interlocked.Increment(ref orderItemWasAdded));
        }
    }
}