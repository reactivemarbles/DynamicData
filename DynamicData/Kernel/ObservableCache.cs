using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Operators;

namespace DynamicData.Kernel
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal sealed class ObservableCache<TObject, TKey> :  IObservableCache<TObject, TKey>
                                     
    {


        #region Fields

        private readonly Func<TObject, TKey> _keySelector;
        private readonly ICache<TObject, TKey> _cache = new ConcurrentCache<TObject, TKey>();
        private readonly IDisposable _disposer;
        private readonly object _locker = new object();
        private readonly ISubject<IChangeSet<TObject, TKey>> _updates = new Subject<IChangeSet<TObject, TKey>>();

        #endregion

        #region Construction

        public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
            : this()
        {
            var loader = source.Synchronize(_locker)
                                .Select(updates =>
                                            {
                                                var updater = new IntermediateUpdater<TObject, TKey>(_cache);
                                                updater.Update(updates);
                                                return updater.AsChangeSet();
                                            })
                                .NotEmpty()
                                .Subscribe(_updates.OnNext);

            _disposer = Disposable.Create(() =>
                                              {
                                                  loader.Dispose();
                                                  _updates.OnCompleted();
                                              });
        }

        public ObservableCache(Func<TObject, TKey> keySelector=null)
        {
            _keySelector = keySelector;
            _disposer = Disposable.Create(() => _updates.OnCompleted());
        }

        #endregion

        #region Updating (for internal purposes only)

       internal void UpdateFromIntermediate(Action<IIntermediateUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null) throw new ArgumentNullException("updateAction");

            lock (_locker)
            {
                try
                {
                    var updater = new IntermediateUpdater<TObject, TKey>(_cache);
                    updateAction(updater);
                    var notifications = updater.AsChangeSet();
                    _updates.OnNext(notifications);
                }
                catch (Exception ex)
                {
                    _updates.OnError(ex);
                }
            }
        }


        internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (_keySelector == null) throw new InvalidOperationException("Must construct with keySelector");
            if (updateAction == null) throw new ArgumentNullException("updateAction");

            lock (_locker)
            {
                try
                {
                    var updater = new SourceUpdater<TObject, TKey>(_cache, new KeySelector<TObject, TKey>(_keySelector));
                    updateAction(updater);

                    var notifications = updater.AsChangeSet();
                    if (notifications.Count != 0)
                        _updates.OnNext(notifications);
             }
                catch (Exception ex)
                {
                    _updates.OnError(ex);
                }
            }
        }
        
        #endregion

        #region Accessors
       
        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }

        public IEnumerable<TKey> Keys
        {
            get { return _cache.Keys; }
        }

        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _cache.KeyValues; }
        }

        public IEnumerable<TObject> Items
        {
            get { return _cache.Items; }

        }

        public int Count
        {
            get { return _cache.Count; }
        }

        #endregion
        
        #region Connection

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
                (
                    observer =>
                        {
                            lock (_locker)
                            {
                                Action<Change<TObject, TKey>> nextAction = c =>
                                {
                                    try
                                    {
                                        observer.OnNext(c);
                                    }
                                    catch (Exception ex)
                                    {
                                        observer.OnError(ex);
                                    }
                                };

                                var initial = _cache.Lookup(key);
                                if (initial.HasValue)
                                {
                                    nextAction(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                                }
                                
                                return  _updates.FinallySafe(observer.OnCompleted).Subscribe(changes =>
                                {
                                    var matches = changes.Where(update => update.Key.Equals(key));
                                    foreach (var match in matches)
                                    {
                                        nextAction(match);
                                    }
                                });
                            }
                        });
        }


        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        lock (_locker)
                        {
                            var initial = GetInitialUpdates();
                            if (initial.Count > 0)
                                observer.OnNext(initial);

                            return _updates.FinallySafe(observer.OnCompleted)
                                        .SubscribeSafe(observer);
                        }
                    });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions = null)
        {

            if (filter == null) throw new ArgumentNullException("filter");

            parallelisationOptions = parallelisationOptions ?? new ParallelisationOptions();

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                        {
                            lock (_locker)
                            {
                                var filterer = new DefaultFilterer<TObject, TKey>(filter, parallelisationOptions);
                                var filtered = filterer.Filter(GetInitialUpdates());
                                if (filtered.Count!=0)
                                    observer.OnNext(filtered);

                                return _updates
                                    .FinallySafe(observer.OnCompleted)
                                    .Select(filterer.Filter)
                                    .NotEmpty()
                                    .SubscribeSafe(observer);
                            }
                        });
        }



        internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null)
        {
            lock (_locker)
            {
               return  _cache.AsInitialUpdates(filter);
           }
        }
        #endregion

        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}