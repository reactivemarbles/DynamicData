// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;

namespace DynamicData.Experimental
{
    /// <summary>
    /// Experimental operator extensions.
    /// </summary>
    public static class ExperimentalEx
    {
        /// <summary>
        /// Wraps the source cache, optimising it for watching individual updates.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>The watcher.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IWatcher<TObject, TKey> AsWatcher<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IScheduler? scheduler = null)
            where TKey : notnull
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new Watcher<TObject, TKey>(source, scheduler ?? Scheduler.Default);
        }
    }
}