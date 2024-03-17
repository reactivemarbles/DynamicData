// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class BatchIf<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut, bool initialPauseState = false, IObservable<Unit>? intervalTimer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<bool> _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));

    private readonly IScheduler _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<ChangeSet<TObject, TKey>> Run() => Observable.Create<ChangeSet<TObject, TKey>>(
            observer =>
            {
                var batchedChanges = new List<IChangeSet<TObject, TKey>>();
                var locker = new object();
                var paused = initialPauseState;
                var timeoutDisposer = new SerialDisposable();
                var intervalTimerDisposer = new SerialDisposable();

                void ResumeAction()
                {
                    if (batchedChanges.Count == 0)
                    {
                        return;
                    }

                    var resultingBatch = new ChangeSet<TObject, TKey>(batchedChanges.Sum(cs => cs.Count));
                    foreach (var cs in batchedChanges)
                    {
                        resultingBatch.AddRange(cs);
                    }

                    observer.OnNext(resultingBatch);
                    batchedChanges.Clear();
                }

                IDisposable IntervalFunction() =>
                    intervalTimer.Synchronize(locker).Finally(() => paused = false).Subscribe(
                        _ =>
                        {
                            paused = false;
                            ResumeAction();
                            if (intervalTimer is not null)
                            {
                                paused = true;
                            }
                        });

                if (intervalTimer is not null)
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
                            if (timeOut.HasValue)
                            {
                                timeoutDisposer.Disposable = Disposable.Empty;
                            }

                            ResumeAction();
                        }
                        else if (timeOut.HasValue)
                        {
                            timeoutDisposer.Disposable = Observable.Timer(timeOut.Value, _scheduler).Synchronize(locker).Subscribe(
                                _ =>
                                {
                                    paused = false;
                                    ResumeAction();
                                });
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
