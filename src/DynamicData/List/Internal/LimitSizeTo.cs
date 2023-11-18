// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class LimitSizeTo<T>(ISourceList<T> sourceList, int sizeLimit, IScheduler scheduler, object locker)
    where T : notnull
{
    private readonly IScheduler _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    private readonly ISourceList<T> _sourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));

    public IObservable<IEnumerable<T>> Run()
    {
        var emptyResult = new List<T>();
        long orderItemWasAdded = -1;

        return _sourceList.Connect().ObserveOn(_scheduler).Synchronize(locker).Transform(t => new ExpirableItem<T>(t, _scheduler.Now.UtcDateTime, Interlocked.Increment(ref orderItemWasAdded))).ToCollection().Select(
            list =>
            {
                var numberToExpire = list.Count - sizeLimit;
                if (numberToExpire < 0)
                {
                    return emptyResult;
                }

                return list.OrderBy(exp => exp.ExpireAt).ThenBy(exp => exp.Index).Take(numberToExpire).Select(item => item.Item).ToList();
            }).Where(items => items.Count != 0);
    }
}
