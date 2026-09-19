// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Filter class.
/// </summary>
internal static partial class Filter
{
/// <summary>
/// Provides members for the Static class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
public static class Static<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        /// <summary>
        /// Executes the Create operation.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="filter">The filter value.</param>
        /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
        /// <returns>The result of the operation.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, bool> filter,
            bool suppressEmptyChangeSets)
        {
            ArgumentExceptionHelper.ThrowIfNull(source);
            ArgumentExceptionHelper.ThrowIfNull(filter);

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
