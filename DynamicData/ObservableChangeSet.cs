using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData
{
    /// <summary>
    /// Creation methods for observable change sets
    /// </summary>
    public static class ObservableChangeSet
    {

        #region Cache Create Methods

        /// <summary>
        /// Creates an observable cache from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Action> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            return Create(cache =>
            {
                var action = subscribe(cache);
                return Disposable.Create(() => { action?.Invoke(); });
            },keySelector);
        }

        /// <summary>
        /// Creates an observable cache from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, IDisposable> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                var disposable = new SingleAssignmentDisposable();

                try
                {
                    disposable.Disposable = subscribe(cache);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(cache.Connect().SubscribeSafe(observer), 
                    cache, 
                    Disposable.Create(observer.OnCompleted));
            });
        }

        /// <summary>
        /// Creates an observable cache from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>                                                                                                      
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task<IDisposable>> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Create(async (list, ct) => await subscribe(list), keySelector);
        }

        /// <summary>
        /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task<IDisposable>> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(async (observer, ct) =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                var disposable = new SingleAssignmentDisposable();

                try
                {
                    disposable.Disposable = await subscribe(cache, ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(cache.Connect().SubscribeSafe(observer), 
                    cache, 
                    disposable, 
                    Disposable.Create(observer.OnCompleted));
            });
        }

        /// <summary>
        /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task<Action>> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Create(async (list, ct) => await subscribe(list),keySelector);
        }


        /// <summary>
        /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task<Action>> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(async (observer, ct) =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                Action disposeAction = null;

                try
                {
                    disposeAction = await subscribe(cache, ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(cache.Connect().SubscribeSafe(observer), 
                    cache, Disposable.Create(() =>
                {
                    observer.OnCompleted();
                    disposeAction?.Invoke();
                }));
            });
        }

        /// <summary>
        /// Creates an observable cache from a specified asynchronous Subscribe method. 
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(async observer =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);

                try
                {
                    await subscribe(cache);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(cache.Connect().SubscribeSafe(observer),
                    cache, 
                    Disposable.Create(observer.OnCompleted));
            });
        }

        /// <summary>
        /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
        /// </summary>
        /// <typeparam name="TObject">The type of the elements contained in the observable cache</typeparam>
        /// <typeparam name="TKey">The type of the specified key</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task> subscribe, Func<TObject, TKey> keySelector)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(async (observer, ct) =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);

                try
                {
                    await subscribe(cache, ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(cache.Connect().SubscribeSafe(observer), 
                    cache, 
                    Disposable.Create(observer.OnCompleted));
            });
        }


        #endregion

        #region List Create Methods

        /// <summary>
        /// Creates an observable list from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Action> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Create<T>(list =>
            {
                var action = subscribe(list);
                return Disposable.Create(() => { action?.Invoke(); });
            });
        }

        /// <summary>
        /// Creates an observable list from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, IDisposable> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var list = new SourceList<T>();
                IDisposable disposeAction = null;

                try
                {
                    disposeAction = subscribe(list);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(list.Connect().SubscribeSafe(observer), list, Disposable.Create(() =>
                {
                    observer.OnCompleted();
                    disposeAction?.Dispose();
                }));
            });
        }

        /// <summary>
        /// Creates an observable list from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>                                                                                                        
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task<IDisposable>> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            return Create<T>(async (list, ct) => await subscribe(list));
        }

        /// <summary>
        /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<IDisposable>> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Observable.Create<IChangeSet<T>>(async (observer, ct) =>
            {
                var list = new SourceList<T>();
                IDisposable disposeAction = null;
                SingleAssignmentDisposable actionDisposable = new SingleAssignmentDisposable();

                try
                {
                    disposeAction = await subscribe(list, ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(list.Connect().SubscribeSafe(observer), list, actionDisposable, Disposable.Create(() =>
                {
                    observer.OnCompleted();
                    disposeAction?.Dispose();
                }));
            });
        }

        /// <summary>
        /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>,  Task<Action>> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            return Create<T>(async (list, ct) => await subscribe(list));
        }


        /// <summary>
        /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<Action>> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Observable.Create<IChangeSet<T>>(async (observer, ct) =>
            {
                var list = new SourceList<T>();
                Action disposeAction = null;

                try
                {
                    disposeAction = await subscribe(list, ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(list.Connect().SubscribeSafe(observer), list, Disposable.Create(() =>
                {
                    observer.OnCompleted();
                    disposeAction?.Invoke();
                }));
            });
        }

        /// <summary>
        /// Creates an observable list from a specified asynchronous Subscribe method. 
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Observable.Create<IChangeSet<T>>(async observer =>
            {
                var list = new SourceList<T>();

                try
                {
                     await subscribe(list);
                    list.OnCompleted();
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(list.Connect().SubscribeSafe(observer), list, Disposable.Create(observer.OnCompleted));
            });
        }

        /// <summary>
        /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
        /// </summary>
        /// <typeparam name="T">The type of the elements contained in the observable list</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
        /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task> subscribe)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            return Observable.Create<IChangeSet<T>>(async (observer, ct) =>
            {
                var list = new SourceList<T>();

                try
                {
                     await subscribe(list,ct);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(list.Connect().SubscribeSafe(observer), list, Disposable.Create(observer.OnCompleted));
            });
        }


        #endregion
    }
}