// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the RefCount class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
internal sealed class RefCount<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _list field.
    /// </summary>
    private IObservableList<T>? _list;

    /// <summary>
    /// The _refCount field.
    /// </summary>
    private int _refCount;

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                lock (_locker)
                {
                    if (++_refCount == 1)
                    {
                        _list = source.AsObservableList();
                    }
                }

                if (_list is null)
                {
                    throw new InvalidOperationException("The list is null despite having reference counting.");
                }

                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();
                        IDisposable? listToDispose = null;
                        lock (_locker)
                        {
                            if (--_refCount == 0)
                            {
                                listToDispose = _list;
                                _list = null;
                            }
                        }

                        listToDispose?.Dispose();
                    });
            });
}
