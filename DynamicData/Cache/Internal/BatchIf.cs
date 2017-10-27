using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Cache.Internal
{
    internal sealed class BatchIf<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly IObservable<bool> _pauseIfTrueSelector;
        private readonly IObservable<Unit> _timer;
        private readonly bool _intialPauseState;
        private readonly IScheduler _scheduler;

        public BatchIf(IObservable<IChangeSet<TObject, TKey>> source,
                       IObservable<bool> pauseIfTrueSelector,
                       IObservable<Unit> timer,
                       bool intialPauseState = false,
                       IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _intialPauseState = intialPauseState;
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
            (
                observer =>
                {
                    var buffer = _intialPauseState;
                    var locker = new object();
                    var pulse = new Subject<Unit>();
                    var buffered = new Subject<IChangeSet<TObject, TKey>>();
                    var unbuffered = new Subject<IChangeSet<TObject, TKey>>();
                    var bufferClosing = _timer.Finally(() => buffer = false /*No more buffering*/)
                                              .Merge(pulse);

                    var pauseSignal = Observable.Return(_intialPauseState)
                                                .Concat(_pauseIfTrueSelector)
                                                .ObserveOn(_scheduler)
                                                .Synchronize(locker)
                                                .Subscribe(paused =>
                                                           {
                                                               buffer = paused;

                                                               if (!buffer)
                                                               {
                                                                   //Make sure we notify the buffer to empty
                                                                   //anything it has buffered
                                                                   pulse.OnNext(Unit.Default);
                                                               }
                                                           });

                    var observerSubscription = buffered.Buffer(bufferClosing)
                                                       .FlattenBufferResult()
                                                       .Merge(unbuffered)
                                                       .Subscribe(observer);

                    var sourceSubscription = _source.Synchronize(locker)
                                                    .Subscribe(update =>
                                                               {
                                                                   //Buffer or unbuffered observable?
                                                                   var target = buffer ? buffered : unbuffered;

                                                                   target.OnNext(update);
                                                               });

                    return new CompositeDisposable(buffered,
                                                   unbuffered,
                                                   pulse,
                                                   pauseSignal,
                                                   observerSubscription,
                                                   sourceSubscription);
                }
            );
        }
    }
}