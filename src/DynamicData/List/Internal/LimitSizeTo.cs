// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;

namespace DynamicData.List.Internal;

internal sealed class LimitSizeTo<T>
    where T : notnull
{
    private readonly object _locker;

    private readonly IScheduler _scheduler;

    private readonly int _sizeLimit;

    private readonly ISourceList<T> _sourceList;

    public LimitSizeTo(ISourceList<T> sourceList, int sizeLimit, IScheduler scheduler, object locker)
    {
        _sourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));
        _sizeLimit = sizeLimit;
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _locker = locker;
    }

    public IObservable<IEnumerable<T>> Run()
    {
        var emptyResult = new List<T>();
        long orderItemWasAdded = -1;

        return _sourceList.Connect().ObserveOn(_scheduler).Synchronize(_locker).Transform(t => new ExpirableItem<T>(t, _scheduler.Now.UtcDateTime, Interlocked.Increment(ref orderItemWasAdded))).ToCollection().Select(
            list =>
            {
                var numberToExpire = list.Count - _sizeLimit;
                if (numberToExpire < 0)
                {
                    return emptyResult;
                }

                return list.OrderBy(exp => exp.ExpireAt).ThenBy(exp => exp.Index).Take(numberToExpire).Select(item => item.Item).ToList();
            }).Where(items => items.Count != 0);
    }
}
