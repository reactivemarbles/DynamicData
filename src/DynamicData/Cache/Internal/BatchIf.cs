// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal sealed class BatchIf<TObject, TKey>
        where TKey : notnull
    {
        private readonly bool _initialPauseState;

        private readonly IObservable<Unit>? _intervalTimer;

        private readonly IObservable<bool> _pauseIfTrueSelector;

        private readonly IScheduler _scheduler;

        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        private readonly TimeSpan? _timeOut;

        public BatchIf(IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut, bool initialPauseState = false, IObservable<Unit>? intervalTimer = null, IScheduler? scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            _timeOut = timeOut;
            _initialPauseState = initialPauseState;
            _intervalTimer = intervalTimer;
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public IObservable<ChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<ChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var batchedChanges = new List<IChangeSet<TObject, TKey>>();
                        var locker = new object();
                        var paused = _initialPauseState;
                        var timeoutDisposer = new SerialDisposable();
                        var intervalTimerDisposer = new SerialDisposable();

                        void ResumeAction()
                        {
                            if (batchedChanges.Count == 0)
                            {
                                return;
                            }

                            var resultingBatch = new ChangeSet<TObject, TKey>(batchedChanges.Select(cs => cs.Count).Sum());
                            foreach (var cs in batchedChanges)
                            {
                                resultingBatch.AddRange(cs);
                            }

                            observer.OnNext(resultingBatch);
                            batchedChanges.Clear();
                        }

                        IDisposable IntervalFunction()
                        {
                            return _intervalTimer.Synchronize(locker).Finally(() => paused = false).Subscribe(
                                _ =>
                                    {
                                        paused = false;
                                        ResumeAction();
                                        if (_intervalTimer is not null)
                                        {
                                            paused = true;
                                        }
                                    });
                        }

                        if (_intervalTimer is not null)
                        {
                            intervalTimerDisposer.Disposable = IntervalFunction();
                        }

                        var pausedHandler = _pauseIfTrueSelector.Synchronize(locker).Subscribe(
                            p =>
                                {
                                    paused = p;
                                    if (!p)
                                    {
                                        // pause window has closed, so reset timer
                                        if (_timeOut.HasValue)
                                        {
                                            timeoutDisposer.Disposable = Disposable.Empty;
                                        }

                                        ResumeAction();
                                    }
                                    else
                                    {
                                        if (_timeOut.HasValue)
                                        {
                                            timeoutDisposer.Disposable = Observable.Timer(_timeOut.Value, _scheduler).Synchronize(locker).Subscribe(
                                                _ =>
                                                    {
                                                        paused = false;
                                                        ResumeAction();
                                                    });
                                        }
                                    }
                                });

                        var publisher = _source.Synchronize(locker).Subscribe(
                            changes =>
                                {
                                    batchedChanges.Add(changes);

                                    // publish if not paused
                                    if (!paused)
                                    {
                                        ResumeAction();
                                    }
                                });

                        return new CompositeDisposable(publisher, pausedHandler, timeoutDisposer, intervalTimerDisposer);
                    });
        }
    }
}