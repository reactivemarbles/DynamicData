using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal sealed class BatchIf<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly IObservable<bool> _pauseIfTrueSelector;
        private readonly TimeSpan? _timeOut;
        private readonly bool _intialPauseState;
        private readonly IObservable<Unit> _intervalTimer;
        private readonly IScheduler _scheduler;

        public BatchIf(IObservable<IChangeSet<TObject, TKey>> source,
                       IObservable<bool> pauseIfTrueSelector,
                        TimeSpan? timeOut,
                       bool intialPauseState = false,
                        IObservable<Unit> intervalTimer =null,
                       IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            _timeOut = timeOut;
            _intialPauseState = intialPauseState;
            _intervalTimer = intervalTimer;
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
            (
                observer =>
                {
                    var result = new ChangeAwareCache<TObject, TKey>();
                    var locker = new object();
                    var paused = _intialPauseState;
                    var timeoutDisposer = new SerialDisposable();
                    var intervalTimerDisposer = new SerialDisposable();

                    void ResumeAction()
                    {
                        //publish changes (if there are any)
                        var changes = result.CaptureChanges();
                        if (changes.Count > 0) observer.OnNext(changes);
                    }

                    IDisposable IntervalFunction()
                    {
                        return _intervalTimer
                            .Synchronize(locker)
                            .Finally(() => paused = false)
                            .Subscribe(_ =>
                            {
                                paused = false;
                                ResumeAction();
                                if (_intervalTimer!=null)
                                    paused = true;
                            });
                    }

                    if (_intervalTimer != null)
                        intervalTimerDisposer.Disposable = IntervalFunction();

                    var pausedHander = _pauseIfTrueSelector
                      // .StartWith(initalp)
                        .Synchronize(locker)
                        .Subscribe(p =>
                        {
                            paused = p;
                            if (!p)
                            {
                                //pause window has closed, so reset timer 
                               if (_timeOut.HasValue) timeoutDisposer.Disposable = Disposable.Empty;
                                ResumeAction();
                            }
                            else
                            {
                                if (_timeOut.HasValue)
                                    timeoutDisposer.Disposable = Observable.Timer(_timeOut.Value, _scheduler)
                                        .Synchronize(locker)
                                        .Subscribe(_ =>
                                        {
                                            paused = false;
                                            ResumeAction();
                                        });
                            }

                        });

                    var publisher = _source
                        .Synchronize(locker)
                        .Subscribe(changes =>
                        {
                            result.Clone(changes);

                            //publish if not paused
                            if (!paused)
                                observer.OnNext(result.CaptureChanges());
                        });

                    return new CompositeDisposable(publisher, pausedHander, timeoutDisposer, intervalTimerDisposer);
                }
            );
        }
    }
}