// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal static partial class Filter
{
    public static class Static<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, bool> filter,
            bool suppressEmptyChangeSets)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            filter.ThrowArgumentNullExceptionIfNull(nameof(filter));

            return Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver =>
            {
                var downstreamItems = new ChangeAwareCache<TObject, TKey>();

                return source
                    .Select(upstreamChanges =>
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    if (filter.Invoke(change.Current))
                                        downstreamItems.Add(change.Current, change.Key);
                                    break;

                                // Intentionally not supporting Moved changes, too much work to try and track indexes.

                                case ChangeReason.Refresh:
                                    {
                                        var isIncluded = filter.Invoke(change.Current);
                                        var wasIncluded = downstreamItems.Lookup(change.Key).HasValue;

                                        if (isIncluded && !wasIncluded)
                                            downstreamItems.Add(change.Current, change.Key);
                                        else if (isIncluded && wasIncluded)
                                            downstreamItems.Refresh(change.Key);
                                        else if (!isIncluded && wasIncluded)
                                            downstreamItems.Remove(change.Key);
                                    }
                                    break;

                                case ChangeReason.Remove:
                                    downstreamItems.Remove(change.Key);
                                    break;

                                case ChangeReason.Update:
                                    if (filter.Invoke(change.Current))
                                        downstreamItems.AddOrUpdate(change.Current, change.Key);
                                    else
                                        downstreamItems.Remove(change.Key);
                                    break;
                            }
                        }

                        return downstreamItems.CaptureChanges();
                    })
                    .Where(downstreamChanges => !suppressEmptyChangeSets || (downstreamChanges.Count is not 0))
                    .SubscribeSafe(downstreamObserver);
            });
        }
    }
}
