using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Annotations;

namespace DynamicData.List.Internal
{
    internal sealed  class BufferIf<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<bool> _pauseIfTrueSelector;
        private readonly bool _intialPauseState;
        private readonly TimeSpan _timeOut;
        private readonly IScheduler _scheduler;

        public BufferIf([NotNull] IObservable<IChangeSet<T>> source, [NotNull] IObservable<bool> pauseIfTrueSelector,
                        bool intialPauseState = false, TimeSpan? timeOut = null, IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            _intialPauseState = intialPauseState;
            _timeOut = timeOut ?? TimeSpan.Zero;
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>
                (
                    observer =>
                    {
                        var locker = new object();
                        var paused = _intialPauseState;
                        var buffer = new List<Change<T>>();
                        var timeoutSubscriber = new SerialDisposable();
                        var timeoutSubject = new Subject<bool>();

                        var bufferSelector = Observable.Return(_intialPauseState)
                                                       .Concat(_pauseIfTrueSelector.Merge(timeoutSubject))
                                                       .ObserveOn(_scheduler)
                                                       .Synchronize(locker)
                                                       .Publish();

                        var pause = bufferSelector.Where(state => state)
                                                  .Subscribe(_ =>
                                                  {
                                                      paused = true;
                                                      //add pause timeout if required
                                                      if (_timeOut != TimeSpan.Zero)
                                                          timeoutSubscriber.Disposable = Observable.Timer(_timeOut, _scheduler)
                                                                                                   .Select(l => false)
                                                                                                   .SubscribeSafe(timeoutSubject);
                                                  });

                        var resume = bufferSelector.Where(state => !state)
                                                   .Subscribe(_ =>
                                                   {
                                                       paused = false;
                                                       //publish changes and clear buffer
                                                       if (buffer.Count == 0) return;
                                                       observer.OnNext(new ChangeSet<T>(buffer));
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
                    });
        }
    }
}
