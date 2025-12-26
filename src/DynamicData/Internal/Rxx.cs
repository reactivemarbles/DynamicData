// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER
using DynamicData.Internal;

namespace System.Reactive.Linq;

internal static class Rxx
{
    /// <summary>
    /// Keep this class internal as it should be supplied by System.Reactive and probably will be one day.
    /// </summary>
    public static IObservable<T> Synchronize<T>(this IObservable<T> source, Lock locker)
    {
        return Observable.Create<T>(observer =>
        {
            return source.SubscribeSafe(t =>
            {
                lock (locker)
                {
                    observer.OnNext(t);
                }
            }, ex =>
            {
                lock (locker)
                {
                    observer.OnError(ex);
                }
            }, () =>
            {
                lock (locker)
                {
                    observer.OnCompleted();
                }
            });
        });
    }
}
#endif
