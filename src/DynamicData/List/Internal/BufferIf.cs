// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.List.Internal;

internal sealed class BufferIf<T>
{
    private readonly bool _initialPauseState;

    private readonly IObservable<bool> _pauseIfTrueSelector;

    private readonly IScheduler _scheduler;

    private readonly IObservable<IChangeSet<T>> _source;

    private readonly TimeSpan _timeOut;

    public BufferIf(IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
        _initialPauseState = initialPauseState;
        _timeOut = timeOut ?? TimeSpan.Zero;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    public IObservable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();
                var paused = _initialPauseState;
                var buffer = new ChangeSet<T>();
                var timeoutSubscriber = new SerialDisposable();
                var timeoutSubject = new Subject<bool>();

                var bufferSelector = Observable.Return(_initialPauseState).Concat(_pauseIfTrueSelector.Merge(timeoutSubject)).ObserveOn(_scheduler).Synchronize(locker).Publish();

                var pause = bufferSelector.Where(state => state).Subscribe(
                    _ =>
                    {
                        paused = true;

                        // add pause timeout if required
                        if (_timeOut != TimeSpan.Zero)
                        {
                            timeoutSubscriber.Disposable = Observable.Timer(_timeOut, _scheduler).Select(_ => false).SubscribeSafe(timeoutSubject);
                        }
                    });

                var resume = bufferSelector.Where(state => !state).Subscribe(
                    _ =>
                    {
                        paused = false;

                        // publish changes and clear buffer
                        if (buffer.Count == 0)
                        {
                            return;
                        }

                        observer.OnNext(buffer);
                        buffer = new ChangeSet<T>();

                        // kill off timeout if required
                        timeoutSubscriber.Disposable = Disposable.Empty;
                    });

                var updateSubscriber = _source.Synchronize(locker).Subscribe(
                    updates =>
                    {
                        if (paused)
                        {
                            buffer.AddRange(updates);
                        }
                        else
                        {
                            observer.OnNext(updates);
                        }
                    });

                var connected = bufferSelector.Connect();

                return Disposable.Create(
                    () =>
                    {
                        connected.Dispose();
                        pause.Dispose();
                        resume.Dispose();
                        updateSubscriber.Dispose();
                        timeoutSubject.OnCompleted();
                        timeoutSubscriber.Dispose();
                    });
            });
    }
}
