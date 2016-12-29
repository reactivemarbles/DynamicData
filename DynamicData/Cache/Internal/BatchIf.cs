using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Cache.Internal
{
    internal class BatchIf<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly IObservable<bool> _pauseIfTrueSelector;
        private readonly bool _intialPauseState;
        private readonly TimeSpan? _timeOut;
        private readonly IScheduler _scheduler;

        public BatchIf(IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<bool> pauseIfTrueSelector,
            bool intialPauseState = false,
            TimeSpan? timeOut = null,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));

            _source = source;
            _pauseIfTrueSelector = pauseIfTrueSelector;
            _intialPauseState = intialPauseState;
            _timeOut = timeOut;
            _scheduler = scheduler;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        bool paused = _intialPauseState;
                        var locker = new object();
                        var buffer = new List<Change<TObject, TKey>>();
                        var timeoutSubscriber = new SerialDisposable();
                        var timeoutSubject = new Subject<bool>();

                        var schedulertouse = _scheduler ?? Scheduler.Default;

                        var bufferSelector = Observable.Return(_intialPauseState)
                            .Concat(_pauseIfTrueSelector.Merge(timeoutSubject))
                            .ObserveOn(schedulertouse)
                            .Synchronize(locker)
                            .Publish();

                        var pause = bufferSelector.Where(shouldPause => shouldPause)
                            .Subscribe(_ =>
                            {
                                paused = true;
                                //add pause timeout if required
                                if (_timeOut != null && _timeOut.Value != TimeSpan.Zero)
                                    timeoutSubscriber.Disposable = Observable.Timer(_timeOut.Value, schedulertouse)
                                        .Select(l => false)
                                        .SubscribeSafe(timeoutSubject);
                            });

                        var resume = bufferSelector.Where(shouldPause => !shouldPause)
                            .Subscribe(_ =>
                            {
                                paused = false;
                                //publish changes and clear buffer
                                if (buffer.Count == 0) return;
                                observer.OnNext(new ChangeSet<TObject, TKey>(buffer));
                                buffer.Clear();

                                //kill off timeout if required
                                timeoutSubscriber.Disposable = Disposable.Empty;
                            });

                        var updateSubscriber = _source.Synchronize(locker)
                            .Subscribe(updates =>
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

                        return Disposable.Create(() =>
                        {
                            connected.Dispose();
                            pause.Dispose();
                            resume.Dispose();
                            updateSubscriber.Dispose();
                            timeoutSubject.OnCompleted();
                            timeoutSubscriber.Dispose();
                        });
                    }
                );
        }
    }
}