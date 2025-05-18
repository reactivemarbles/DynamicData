// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET9_0_OR_GREATER

namespace System.Reactive.Linq;

internal static class Rxx
{
    public static IObservable<T> Synchronize<T>(this IObservable<T> source, Lock locker) =>
        new Synchronize<T>(source, locker).Run();
}

/// <summary>
/// Keep this class internal as it should be supplied by System.Reactive and probably will be one day.
/// </summary>
internal sealed class Synchronize<T>
{
    private readonly IObservable<T> _source;
    private readonly Lock _locker;

    public Synchronize(IObservable<T> source, Lock locker)
    {
        _source = source;
        _locker = locker;
    }

    public Synchronize(IObservable<T> source)
    {
        _source = source;
        _locker = new Lock();
    }

    public IObservable<T> Run()
    {
        return Observable.Create<T>(observer =>
        {
            return _source.Subscribe(t =>
            {
                lock (_locker)
                {
                    observer.OnNext(t);
                }
            }, ex =>
            {
                lock (_locker)
                {
                    observer.OnError(ex);
                }
            }, () =>
            {
                lock (_locker)
                {
                    observer.OnCompleted();
                }
            });
        });
    }
}
#endif
