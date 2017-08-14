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
        /// <summary>
        /// Creates an observable sequence from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
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
        /// Creates an observable sequence from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, IDisposable> subscribe)
        {
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
        /// Creates an observable sequence from a specified Subscribe method implementation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>                                                                                                        
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task<IDisposable>> subscribe)
        {
            return Create<T>(async (list, ct) => await subscribe(list));
        }

        /// <summary>
        /// Creates an observable sequence from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<IDisposable>> subscribe)
        {
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
        /// Creates an observable sequence from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>,  Task<Action>> subscribe)
        {
            return Create<T>(async (list, ct) => await subscribe(list));
        }


        /// <summary>
        /// Creates an observable sequence from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<Action>> subscribe)
        {

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
        /// Creates an observable sequence from a specified asynchronous Subscribe method. 
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task> subscribe)
        {

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
        /// Creates an observable sequence from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the produced sequence.</typeparam>
        /// <param name="subscribe">  Implementation of the resulting observable sequence's Subscribe method. </param>
        /// <returns>The observable sequence with the specified implementation for the Subscribe method.</returns>
        public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task> subscribe)
        {

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







        //public static IObservable<IChangeSet<T, V>> Create<T, V>(Func<ISourceCache<T, V>, IDisposable> subscribe, Func<T, V> keySelector)
        //{
        //    var sourceCache = new SourceCache<T, V>(keySelector);

        //    throw new NotImplementedException();
        //}
        //public static IObservable<IChangeSet<T, V>> Create<T, V>(Func<ISourceCache<T, V>, Action> subscribe)
        //{
        //    throw new NotImplementedException();
        //}

        //public static IObservable<IChangeSet<T, V>> Create<T, V>(Func<ISourceCache<T, V>, Task<Action>> subscribe)
        //{
        //    throw new NotImplementedException();
        //}
    }
}