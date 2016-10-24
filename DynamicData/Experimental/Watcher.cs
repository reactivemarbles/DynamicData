#region

using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;

#endregion

namespace DynamicData.Experimental
{
    internal sealed class Watcher<TObject, TKey> : IWatcher<TObject, TKey>
    {
        private readonly IScheduler _scheduler;
        private readonly IntermediateCache<SubjectWithRefCount<Change<TObject, TKey>>, TKey> _subscribers = new IntermediateCache<SubjectWithRefCount<Change<TObject, TKey>>, TKey>();
        private readonly IObservableCache<TObject, TKey> _source;
        private readonly object _locker = new object();

        private readonly IDisposable _disposer;

        public Watcher(IObservable<IChangeSet<TObject, TKey>> source, IScheduler scheduler)
        {
            _scheduler = scheduler;
            _source = source.AsObservableCache();

            var onCompletePublisher = _subscribers.Connect()
                                                  .Synchronize(_locker)
                                                  .ObserveOn(_scheduler)
                                                  .SubscribeMany((t, k) => Disposable.Create(t.OnCompleted))
                                                  .Subscribe();

            var sourceSubscriber = source.Synchronize(_locker).Subscribe(updates => updates.ForEach(update =>
            {
                var subscriber = _subscribers.Lookup(update.Key);
                if (subscriber.HasValue)
                {
                    _scheduler.Schedule(() => subscriber.Value.OnNext(update));
                }
            }));

            _disposer = Disposable.Create(() =>
            {
                onCompletePublisher.Dispose();
                sourceSubscriber.Dispose();

                _source.Dispose();
                _subscribers.Dispose();
            });
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
                (
                    observer =>
                    {
                        lock (_locker)
                        {
                            //Create or find the existing subscribers
                            var existing = _subscribers.Lookup(key);
                            SubjectWithRefCount<Change<TObject, TKey>> subject;
                            if (existing.HasValue)
                            {
                                subject = existing.Value;
                            }
                            else
                            {
                                subject = new SubjectWithRefCount<Change<TObject, TKey>>(new ReplaySubject<Change<TObject, TKey>>(1));

                                var initial = _source.Lookup(key);
                                if (initial.HasValue)
                                {
                                    var update = new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value);
                                    subject.OnNext(update);
                                }
                                _subscribers.Edit(updater => updater.AddOrUpdate(subject, key));
                            }

                            //set up subscription
                            var subscriber = subject.SubscribeSafe(observer);

                            return Disposable.Create(() =>
                            {
                                //lock to ensure no race condition where the same key could be subscribed
                                //to whilst disposal is taking place
                                lock (_locker)
                                {
                                    subscriber.Dispose();
                                    if (subject.RefCount == 0)
                                    {
                                        _subscribers.Remove(key);
                                    }
                                }
                            });
                        }
                    });
        }

        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}
