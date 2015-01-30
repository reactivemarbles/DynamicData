#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Kernel;
using DynamicData.Operators;

#endregion

namespace DynamicData
{

    /// <summary>
    /// Extensions for dynamic data
    /// </summary>
    public static class DynamicDataEx
    {
        #region Error Handling

        /// <summary>
        /// Subscribes an element handler observable sequence and calls back with the exception
        ///  
        /// Use in combination with Finally() to catch the completion handler
        /// </summary>
        /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
        /// <param name="source">Connector sequence to subscribe to.</param>
        /// <param name="subscribeAction">The subscribe action.</param>
        /// <param name="errorAction">The error action.</param>
        /// <remarks>
        /// The stream will be disposed on errors
        /// </remarks>
        /// <returns>
        /// IDisposable object used to unsubscribe from the observable sequence.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IDisposable SubscribeAndCatch<T>(this IObservable<T> source, Action<T> subscribeAction,
            Action<Exception> errorAction)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (subscribeAction == null) throw new ArgumentNullException("subscribeAction");
            if (errorAction == null) throw new ArgumentNullException("errorAction");

            return Observable.Create<T>(o => source.FinallySafe(o.OnCompleted)
                .Subscribe(t =>
                           {
                               try
                               {
                                   subscribeAction(t);
                               }
                               catch (Exception ex)
                               {
                                   errorAction(ex);
                                   o.OnCompleted();
                               }
                           }, o.OnError, o.OnCompleted)).Subscribe();
        }

        /// <summary>
        /// Ensure that finally is always called. Thanks to Lee Campbell for this
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="finallyAction">The finally action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (finallyAction == null) throw new ArgumentNullException("finallyAction");

            return Observable.Create<T>(o =>
                                        {
                                            var finallyOnce = Disposable.Create(finallyAction);

                                            var subscription = source.Subscribe(o.OnNext,
                                                ex =>
                                                {
                                                    try
                                                    {
                                                        o.OnError(ex);
                                                    }
                                                    finally
                                                    {
                                                        finallyOnce.Dispose();
                                                    }
                                                },
                                                () =>
                                                {
                                                    try
                                                    {
                                                        o.OnCompleted();
                                                    }
                                                    finally
                                                    {
                                                        finallyOnce.Dispose();
                                                    }
                                                });

                                            return new CompositeDisposable(subscription, finallyOnce);

                                        });
        }

        #endregion

        #region General


        /// <summary>
        /// Cache equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> CacheOnDemand<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");

            int refCount = 0;
            var locker = new object();
            IObservableCache<TObject, TKey> cache = null;

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                 {
                    lock (locker)
                     {
                         refCount++;
                         if (refCount == 1)
                         {
                             cache = source.AsObservableCache();
                         }

                        // ReSharper disable once PossibleNullReferenceException (never the case!)
                         var subscriber = cache.Connect().SubscribeSafe(observer);

                         return Disposable.Create(() =>
                         {
                             lock (locker)
                             {
                                 refCount--;
                                 subscriber.Dispose();
                                 if (refCount != 0) return;
                                 cache.Dispose();
                                 cache = null;
                             }
                         });
                     }
                    });

        }


        /// <summary>
        /// Subscribe safe which ensures error handing is passed up the chain
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observer">The observer.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IDisposable SubscribeSafer<T>(this IObservable<T> source, IObserver<T> observer)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.SubscribeSafe(observer);
        }


        /// <summary>
        /// Monitors the status of a stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T>  source)
        {
            if (source == null) throw new ArgumentNullException("source");

            return Observable.Create<ConnectionStatus>(observer =>
                {
                    var statusSubject = new Subject<ConnectionStatus>();
                    var status = ConnectionStatus.Pending;

                    Action<Exception> error = (ex) =>
                        {
                            status = ConnectionStatus.Errored;
                            statusSubject.OnNext(status);
                            observer.OnError(ex);
                        };

                    Action completion = () =>
                    {
                        if (status == ConnectionStatus.Errored) return;
                        status = ConnectionStatus.Completed;
                        statusSubject.OnNext(status);
                    };

                    Action updated = () =>
                    {
                        if (status != ConnectionStatus.Pending) return;
                        status = ConnectionStatus.Loaded;
                        statusSubject.OnNext(status);
                    };

                    var monitor = source.Subscribe(_ => updated(), error,completion);

                    var subscriber = statusSubject
                                .StartWith(status)
                                .DistinctUntilChanged()
                                .SubscribeSafer(observer);

                    return Disposable.Create(() =>
                        {
                            statusSubject.OnCompleted();
                            monitor.Dispose();
                            subscriber.Dispose();
                        });
                });
        }


        /// <summary>
        /// Changes the unique key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey,TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source,
            Func<TObject, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return source.Select(updates =>
                                     {
                                         var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                                         return new ChangeSet<TObject, TDestinationKey>(changed);
                                     });
        }

        /// <summary>
        /// Supresses updates which are empty
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Where(updates => updates.Count!=0);
        }

        /// <summary>
        /// Flattens an update collection to it's individual items
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.SelectMany(updates => updates);
        }

        /// <summary>
        /// Ignores the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="ignoreFunction">The ignore function (current,previous)=>{ return true to ignore }.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> IgnoreUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TObject, bool> ignoreFunction)
        {

            return source.Select(updates =>
            {
                var result = updates.Where(u =>
                {
                    if (u.Reason != ChangeReason.Update)
                    {
                        return true;
                    }
                    return !ignoreFunction(u.Current, u.Previous.Value);
                });
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
        }

        /// <summary>
        /// Only includes the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="includeFunction">The include function (current,previous)=>{ return true to include }.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TObject, bool> includeFunction)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (includeFunction == null) throw new ArgumentNullException("includeFunction");
 
            return source.Select(updates =>
            {
                var result = updates.Where(u =>
                {
                    if (u.Reason != ChangeReason.Update) return true;
                    
                    return includeFunction(u.Current, u.Previous.Value);
                });
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (observableSelector == null) throw new ArgumentNullException("observableSelector");

            return Observable.Create<TDestination>
                (
                    observer => source.SubscribeMany(t => observableSelector(t).SubscribeSafe(observer))
                        .Subscribe());
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject,TKey, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (observableSelector == null) throw new ArgumentNullException("observableSelector");

            return Observable.Create<TDestination>
                (
                    observer => source.SubscribeMany((t,v) => observableSelector(t,v).SubscribeSafe(observer))
                        .Subscribe());
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, IDisposable> subscriptionFactory)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (subscriptionFactory == null) throw new ArgumentNullException("subscriptionFactory");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var published = source.Publish();
                        var subscriptions = published
                                            .Transform((t, k) => new SubscriptionContainer<TObject, TKey>(t, k, subscriptionFactory))
                                            .DisposeMany()
                                            .Subscribe();

                        var result = published.SubscribeSafe(observer);
                        var connected = published.Connect();

                        return new CompositeDisposable(subscriptions, connected, result);
                    });
        }


        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, IDisposable> subscriptionFactory)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (subscriptionFactory == null) throw new ArgumentNullException("subscriptionFactory");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var published = source.Publish();
                        var subscriptions = published
                                            .Transform((t, k) => new SubscriptionContainer<TObject, TKey>(t, k, subscriptionFactory))
                                            .DisposeMany()
                                            .Subscribe();

                        var result = published.SubscribeSafe(observer);
                        var connected = published.Connect();

                        return Disposable.Create(() =>
                        {
                            connected.Dispose();
                            subscriptions.Dispose();
                            result.Dispose();

                        });
                    });
        }



        /// <summary>
        /// Callback for each item as and when it is being removed from the stream
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// removeAction
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (removeAction == null) throw new ArgumentNullException("removeAction");
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var disposer = new OnBeingRemoved<TObject, TKey>(t =>
                        {
                            var d = t as IDisposable;
                            if (d != null) d.Dispose();
                        });
                        var subscriber = source
                            .Do(disposer.RegisterForRemoval, observer.OnError)
                            .SubscribeSafer(observer);

                        return Disposable.Create(() =>
                        {
                            subscriber.Dispose();
                            disposer.Dispose();
                        });
                    });
        }

        /// <summary>
        /// Disposes each item when no longer required.
        /// 
        /// NB: Individual items are disposed when removed or replaced. All items
        /// are disposed when the stream is disposed
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>A continuation of the original stream</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.OnItemRemoved(t =>
                                        {
                                            var d = t as IDisposable;
                                            if (d != null) d.Dispose();
                                        });
        }


        /// <summary>
        /// Includes updates for the specified reasons only
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">reasons</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason</exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAre<TObject, TKey>(
                    this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
         {
             if (reasons == null) throw new ArgumentNullException("reasons");
             if (!reasons.Any()) throw new ArgumentException("Must select at least one reason");
            var hashed = new HashSet<ChangeReason>(reasons);

             return source.Select(updates =>
                 {
                     var filtered = updates.Where(u => hashed.Contains(u.Reason));
                     return new ChangeSet<TObject, TKey>(filtered);
                 }).NotEmpty();
         }

        /// <summary>
        /// Excludes updates for the specified reasons
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">reasons</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason</exception>
         public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(
             this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
         {
             if (reasons == null) throw new ArgumentNullException("reasons");
             if (!reasons.Any()) throw new ArgumentException("Must select at least one reason");

             var hashed = new HashSet<ChangeReason>(reasons);

             return source.Select(updates =>
             {
                 var filtered = updates.Where(u => !hashed.Contains(u.Reason));
                 return new ChangeSet<TObject, TKey>(filtered);
             }).NotEmpty();
         }

        #endregion

        #region Convert parameter types

        private static IObservable<IChangeSet<T, T>> Expand<T>(
            this IObservable<IDistinctChangeSet<T>> source)
        {
            return source.Select(s => (IChangeSet<T, T>) s);
        }

        private static IObservable<IDistinctChangeSet<TObject>> Contract<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source)
            where TObject : TKey
        {
            return source.Select(s => (IDistinctChangeSet<TObject>) s);
        }

        #endregion

        #region Delayed Stream


        /// <summary>
        /// Batches the updates for the specified time period
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSpan">The time span.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// scheduler</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                    TimeSpan timeSpan,
                                                                    IScheduler scheduler=null)
        {
            if (source == null) throw new ArgumentNullException("source");

            return source
                .Buffer(timeSpan, scheduler ?? Scheduler.Default)
                .Where(x=>x.Count!=0)
                .Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));

        }



        /// <summary>
        /// Batches the underlying updates if a pause signal has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseStateObservable">The pause / resume state observable.</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIfPaused<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                            IObservable<bool> pauseStateObservable,
                                                            bool intialPauseState = false,
                                                            IScheduler scheduler=null)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (pauseStateObservable == null) throw new ArgumentNullException("pauseStateObservable");
            ;

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        bool paused = intialPauseState;
                        var locker = new object();
                        var buffer = new List<Change<TObject, TKey>>();

                        var pauseState = pauseStateObservable
                                            .ObserveOn(scheduler ?? Scheduler.Default)
                                            .Subscribe(state =>
                                            {
                                                lock (locker)
                                                {
                                                    if (paused == state) return;
                                                    paused = state;
                                                    if (paused) return;
                                                    observer.OnNext(new ChangeSet<TObject, TKey>(buffer));
                                                    buffer.Clear();
                                                }

                                            });

                        var updateSubscriber = source.Synchronize(locker)
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

                        return Disposable.Create(() =>
                        {
                            updateSubscriber.Dispose();
                            pauseState.Dispose();
                        });
                    }
                );

        }

        /// <summary>
        /// Defer the subscribtion until loaded and skip initial changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.DeferUntilLoaded().Skip(1);
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var published = source.Publish();

                       var subscriber = published.MonitorStatus()
                                            .Where(status => status == ConnectionStatus.Loaded)
                                            .Take(1)
                                            .Select(_=>new ChangeSet<TObject,TKey>())
                                            .Concat(source)
                                            .NotEmpty()
                                            .SubscribeSafe(observer);

                        var connected = published.Connect();

                        return Disposable.Create(() =>
                        {
                            connected.Dispose();
                            subscriber.Dispose();
                        });
                    }
                );
        }

        #endregion
        
        #region Connector / Stream 


        /// <summary>
        /// Converts the stream feeder to a data cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return new AnomynousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Converts the source to an observable cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return new AnomynousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Creates a stream using the specified controlled filter.
        /// The controlled filter enables dynamic inline changing of the filter.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The controlled filter.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">filterController</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Connect<TObject, TKey>(this IObservableCache<TObject, TKey> source, FilterController<TObject> filterController)
        {
            if (filterController == null) throw new ArgumentNullException("filterController");
            return source.Connect().Filter(filterController);
        }

        
        #endregion



        #region Entire Collection Operators

        private sealed class ObservableWithValue<T>
        {
            private readonly IObservable<T> _source;
            private Optional<T> _latestValue = Optional<T>.None; 
            
            public ObservableWithValue(IObservable<T> source)
            {
                _source = source.Do(value => _latestValue = value).StartWith(default(T));
            }
            
            public Optional<T> LatestValue
            {
                get { return _latestValue; }
            }

            public IObservable<T> Observable
            {
                get { return _source; }
            }
        }

        public static IObservable<bool> TrueForAll<TObject, TKey,TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject,IObservable<TValue>> observableSelector,
            Func<TValue,bool> equalityCondition )
        {
            if (source == null) throw new ArgumentNullException("source");
            return Observable.Create<bool>(observer =>
            {

                var transformed = source.Transform(t => new ObservableWithValue<TValue>(observableSelector(t))).Publish();

                IObservable<TValue> inlineChanges = transformed.MergeMany(t => t.Observable);
                IObservable<IEnumerable<ObservableWithValue<TValue>>> queried = transformed.Query(q => q.Items);
               
                //nb: we do not care about the inline change because we are only monitoring it to cause a notification
                var publisher = queried.CombineLatest(inlineChanges, (items, inline) =>
                {
                    return items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value));
                })
                 .DistinctUntilChanged()
                 .SubscribeSafe(observer);


                var connected = transformed.Connect();
                return new CompositeDisposable(connected, publisher);
            });
        }

        /// <summary>
        ///  The latest copy of the cache is exposed for querying after each modification to the underlying data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="resultSelector">The result selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// resultSelector
        /// </exception>
        public static IObservable<TDestination> Query<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");

            return source.Query().Select(resultSelector);
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying after each modification to the underlying data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IQuery<TObject, TKey>> Query<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");

            return Observable.Create<IQuery<TObject, TKey>>
                (
                    observer =>
                    {
                        var cache = new Cache<TObject, TKey>();
                        var query = new AnomynousQuery<TObject, TKey>(cache);

                        return source.Clone(cache).Subscribe(updates =>
                        {
                            try
                            {
                                observer.OnNext(query);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                            }
                        }, observer.OnError, observer.OnCompleted);
                    }
                );
        }




        /// <summary>
        /// Applies a scan function over the dynamic data state
        /// </summary>
        /// <remarks>
        /// The scan function is applied over the cached items after each update is received 
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TAccumulate">The type of the accumulate.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accumulator">The accumulator</param>
        /// <returns></returns>
        public static IObservable<TAccumulate> ScanCache<TObject, TKey, TAccumulate>(this IObservable<IChangeSet<TObject, TKey>> source, 
            TAccumulate seed, 
            Func<IEnumerable<TObject>, TAccumulate> accumulator)
        {
            return source.Query().Select(q=>q.Items.ToList()).Scan(seed, (state, result) => accumulator(result));
        }


        /// <summary>
        /// Applies a scan function over the dynamic data state
        /// </summary>
        /// <remarks>
        /// The scan function is applied over the cached items after each update is received 
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TAccumulate">The type of the accumulate.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accumulator">The accumulator.</param>
        /// <returns></returns>
        public static IObservable<TAccumulate> ScanCache<TObject, TKey, TAccumulate>(this IObservable<IChangeSet<TObject, TKey>> source,
            TAccumulate seed,
            Func<TAccumulate, IEnumerable<TObject>, TAccumulate> accumulator)
        {
            return source.Query().Select(q => q.Items.ToList()).Scan(seed, accumulator);
        }

        /// <summary>
        /// Applies an aggregation function over the dynamic data state, return the result when the sequence ends
        /// </summary>
        /// <remarks>
        /// The scan function is applied over the cached items after each update is received 
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TAccumulate">The type of the accumulate.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accumulator">The accumulator.</param>
        /// <returns></returns>
        public static IObservable<TAccumulate> AggregateCache<TObject, TKey, TAccumulate>(this IObservable<IChangeSet<TObject, TKey>> source,
            TAccumulate seed,
            Func<IEnumerable<TObject>, TAccumulate> accumulator)
        {
            return source.Query().Select(q => q.Items).Aggregate(seed, (state, result) => accumulator(result));
        }


        /// <summary>
        /// Applies an aggregation function over the dynamic data state, return the result when the sequence ends
        /// </summary>
        /// <remarks>
        /// The scan function is applied over the cached items after each update is received 
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TAccumulate">The type of the accumulate.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="accumulator">The accumulator.</param>
        /// <returns></returns>
        public static IObservable<TAccumulate> AggregateCache<TObject, TKey, TAccumulate>(this IObservable<IChangeSet<TObject, TKey>> source,
            TAccumulate seed,
            Func<TAccumulate, IEnumerable<TObject>, TAccumulate> accumulator)
        {
            return source.Query().Select(q => q.Items).Aggregate(seed, accumulator);
        }



        #endregion

        #region Watch

        /// <summary>
        /// Watches updates for a single value matching the specified key
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Watch(key).Select(u => u.Current);
        }
        /// <summary>
        /// Watches updates for a single value matching the specified key
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Watch(key).Select(u => u.Current);
        }
        /// <summary>
        /// Returns an observable of any updates which match the specified key,  preceeded with the initital cache state
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
        }

        #endregion
        
        #region Clone



        internal static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ICache<TObject,TKey> cache)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (cache == null) throw new ArgumentNullException("cache");
            
            return Observable.Create<IChangeSet<TObject, TKey>>
                                (
                                    observer => source
                                        .Do(cache.Clone)
                                        .NotEmpty()
                                        .SubscribeSafe(observer));
                        }

        internal static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var cache = new Cache<TObject, TKey>();
                        return source
                             .NotEmpty()
                             .Do(cache.Clone)
                             .SubscribeSafe(observer);

                    });

        }

        #endregion

        #region Auto removal


        /// <summary>
        /// Automatically removes items from the stream after the time specified by
        /// the timeSelector elapses.  Return null if the item should never be removed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">The time selector.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// timeSelector
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRemove<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
               Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (timeSelector == null) throw new ArgumentNullException("timeSelector");

            return source.AutoRemove(timeSelector, null, scheduler);
        }


        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRemove<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval)
        {
            return AutoRemove<TObject, TKey>(source, timeSelector, pollingInterval, null);
        }

        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> AutoRemove<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (timeSelector == null) throw new ArgumentNullException("timeSelector");

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
              //  var dateTime = DateTime.Now;
                scheduler = scheduler ?? Scheduler.Default;
                var cache = new IntermediateCache<TObject, TKey>(source);
                
                  var published = cache.Connect().Publish();
                var subscriber = published.SubscribeSafe(observer);


                var autoRemover = published.ForAutoRemove(timeSelector, pollingInterval, scheduler)
                            .FinallySafe(observer.OnCompleted)
                            .Subscribe(keys =>
                            {
                                try
                                {
                                    cache.BatchUpdate(updater => updater.Remove(keys.Select(kv => kv.Key)));
                                }
                                catch (Exception ex)
                                {
                                    observer.OnError(ex);
                                }
                            });

                var connected = published.Connect();

                return Disposable.Create(() =>
                {
                    connected.Dispose();
                    subscriber.Dispose();
                    autoRemover.Dispose();
                    cache.Dispose();
                });

            });
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="interval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        internal static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> ForAutoRemove<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (timeSelector == null) throw new ArgumentNullException("timeSelector");

            return Observable.Create<IEnumerable<KeyValuePair<TKey,TObject>>>(observer =>
            {
                var dateTime = DateTime.Now;
                scheduler = scheduler ?? Scheduler.Default;

                var autoRemover = source
                    .Do(x => dateTime = scheduler.Now.DateTime)
                    .Transform((t, v) =>
                    {
                        var removeAt = timeSelector(t);
                        var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;
                        return new ExpirableItem<TObject, TKey>(t, v, expireAt);
                    })
                    .AsObservableCache();

                Action removalAction = () =>
                {
                    try
                    {
                        var toRemove = autoRemover.KeyValues
                            .Where(kv => kv.Value.ExpireAt <= scheduler.Now.DateTime)
                            .ToList();
                       
                        observer.OnNext(toRemove.Select(kv => new KeyValuePair<TKey,TObject>(kv.Key, kv.Value.Value)).ToList());
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                };

                var removalSubscripion = new SingleAssignmentDisposable();
                if (interval.HasValue)
                {
                    // use polling
                    removalSubscripion.Disposable = scheduler.ScheduleRecurringAction(interval.Value, removalAction);
                }
                else
                {
                    //create a timer for each distinct time
                    removalSubscripion.Disposable = autoRemover.Connect()
                        .DistinctValues(ei => ei.ExpireAt)
                        .SubscribeMany(datetime =>
                        {
                            //  Console.WriteLine("Set expiry for {0}. Now={1}", datetime, DateTime.Now);
                            var expireAt = datetime.Subtract(scheduler.Now.DateTime);
                            return Observable.Timer(expireAt, scheduler)
                                .Take(1)
                                .Subscribe(_ => removalAction());
                        })
                        .Subscribe();
                }
                return Disposable.Create(() =>
                {
                    removalSubscripion.Dispose();
                    autoRemover.Dispose();
                });

            });
        }




        /// <summary>
        /// Applies a size limiter to the number of records which can be included in the 
        /// underlying cache.  When the size limit is reached the oldest items are removed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentException">size cannot be zero</exception>
        public static IObservable<IChangeSet<TObject, TKey>> LimitSizeTo<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            int size)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (size <= 0) throw new ArgumentException("Size limit must be greater than zero");

                return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                {
                    var sizeLimiter = new SizeLimiter<TObject, TKey>(size);
                   // var dateTime = DateTime.Now;

                    var root = new IntermediateCache<TObject, TKey>(source);

                    var subscriber = root.Connect()
                        .Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now))
                        .Select(changes =>
                        {
                            var result = sizeLimiter.Update(changes);

                            var removes = result.Where(c => c.Reason == ChangeReason.Remove);
                            root.BatchUpdate(updater => removes.ForEach(c => updater.Remove(c.Key)));
                            return result;
                        })
                        .FinallySafe(observer.OnCompleted)
                        .SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                       // expirableItems.Dispose();
                        subscriber.Dispose();
                        root.Dispose();
                    });
                });

        }

        #endregion

        #region Paged

        /// <summary>
        /// Pages the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="controller">The controller.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IPagedChangeSet<TObject, TKey>> Page<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
            PageController controller)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (controller == null) throw new ArgumentNullException("controller");

            return Observable.Create<IPagedChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var locker = new object();

                        var paginator = new Paginator<TObject, TKey>();
                        var request = controller.Changed.Synchronize(locker).Select(paginator.Paginate);
                        var datachange = source.Synchronize(locker).Select(paginator.Update);

                        return request.Merge(datachange)
                                            .FinallySafe(observer.OnCompleted)
                                            .Where(updates => updates != null)
                                            .SubscribeSafer(observer);
                    });
        }

        #endregion
        
        #region  Filter

        /// <summary>
        /// Filters the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (filter == null)
                return source.Clone();

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                {
                    var filterer = new StaticFilter<TObject, TKey>(filter);
                    return source
                        .Select(filterer.Filter)
                        .NotEmpty()
                        .FinallySafe(observer.OnCompleted)
                        .SubscribeSafer(observer);
                });
        }




        /// <summary>
        /// Creates a stream which can be dynamically controlled.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The filter.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            FilterController<TObject> filterController)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (filterController == null) throw new ArgumentNullException("filterController");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                        {
                            var filterer = new DynamicFilter<TObject, TKey>();
                            var locker = new object();
                            var filter = filterController.FilterChanged.Synchronize(locker).Select(filterer.ApplyFilter);
                            var evaluate = filterController.EvaluateChanged.Synchronize(locker).Select(filterer.Evaluate);
                            var data = source.Synchronize(locker).Select(filterer.Update);

                            return filter.Merge(evaluate).Merge(data)
                                .NotEmpty()
                                .FinallySafe(observer.OnCompleted)
                                .SubscribeSafer(observer);
                        });
        }

        #endregion

        #region Interface aware

        /// <summary>
        /// Updates the index for an object which implements IIndexAware
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TObject : IIndexAware
        {
            return source.Do(updates => 
                updates.SortedItems.Select((update,index)=>new {update,index})
                .ForEach(u => u.update.Value.Index = u.index));

        }


        /// <summary>
        /// Invokes Evaluate method for an object which implements IEvaluateAware
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
                where TObject : IEvaluateAware
        {
            return source
                .Do(
                    updates => updates.Where(u=>u.Reason==ChangeReason.Evaluate)
                    .ForEach(u => u.Current.Evaluate())
                );

        }

        #endregion
        
        #region Sort


        /// <summary>
        /// Sorts using the specified comparer.
        /// Returns the underlying ChangeSet as as per the system conventions.
        /// The resulting changeset also exposes a sorted key value collection of of the underlying cached data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations</param>
        /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sore)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// comparer
        /// </exception>
        public static IObservable<ISortedChangeSet<TObject,TKey>> Sort<TObject,TKey>(this IObservable<IChangeSet<TObject,TKey>> source,
                                                                       IComparer<TObject> comparer, 
                                                                       SortOptimisations sortOptimisations=SortOptimisations.None,
                                                                        int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (comparer == null) throw new ArgumentNullException("comparer");

           return Observable.Create<ISortedChangeSet<TObject,TKey>>
                (
                    observer =>
                    {
                        var sorter = new Sorter<TObject, TKey>(sortOptimisations, comparer, resetThreshold);
                        var locker = new object();
                        return source.Synchronize(locker)
                            .Select(sorter.Sort)
                            .Where(result=>result!=null)
                            .SubscribeSafe(observer);
                    });
        }

        /// <summary>
        /// Sorts a sequence as dictated by the sort controller.
        /// Sequence returns a changeset as as per the system conventions.
        /// Additionally returns a fully sort collection of cached data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sortController">The controlled sort.</param>
        /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations</param>
        /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sore)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">scheduler</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                       SortController<TObject> sortController,
                                                                        SortOptimisations sortOptimisations = SortOptimisations.None,
                                                                        int resetThreshold = -1)
        {

            return Observable.Create<ISortedChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var sorter = new Sorter<TObject, TKey>(sortOptimisations,resetThreshold: resetThreshold);
                        var locker = new object();
                      
                        var comparerChanged = sortController
                            .ComparerChanged
                            .Synchronize(locker).Select(sorter.Sort);

                        var sortAgain = sortController
                            .SortAgain
                            .Synchronize(locker).Select(_ => sorter.Sort());

                        var dataChanged = source.Synchronize(locker)
                                        .Select(sorter.Sort);

                        return comparerChanged
                                .Merge(dataChanged)
                                .Merge(sortAgain)
                                .Where(result => result != null)
                                .FinallySafe(observer.OnCompleted)
                                .SubscribeSafer(observer);
                    });
        }


        #endregion

        #region   Combine

        public static IObservable<IDistinctChangeSet<T>> And<T>(this IObservable<IDistinctChangeSet<T>> source, 
            params IObservable<IDistinctChangeSet<T>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            var parameters = others.Select(s => s.Expand());
            return source.Combine(CombineOperator.ContainedInEach, parameters.ToArray()).Contract();
        }


        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            return source.Combine(CombineOperator.ContainedInEach, others);
        }

        public static IObservable<IDistinctChangeSet<T>> Or<T>(this IObservable<IDistinctChangeSet<T>> source,
            params IObservable<IDistinctChangeSet<T>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            var parameters = others.Select(s => s.Expand());
            return source.Combine(CombineOperator.ContainedInAny, parameters.ToArray()).Contract();
        }


        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            return source.Combine(CombineOperator.ContainedInAny, others);
        }

        public static IObservable<IDistinctChangeSet<T>> Except<T>(this IObservable<IDistinctChangeSet<T>> source,
                params IObservable<IDistinctChangeSet<T>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            var parameters = others.Select(s => s.Expand());
            return source.Combine(CombineOperator.ExceptFor, parameters.ToArray()).Contract();
        }


        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (others == null || others.Length == 0) throw new ArgumentNullException("others");

            return source.Combine(CombineOperator.ExceptFor, others);
        }


        public static IObservable<IChangeSet<TObject, TKey>> Append<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
          TObject item) where TObject:IKey<TKey>
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Append(item, item.Key);
        }
        //TODO: Memory leak with the StartWith operator as it is not a func
        public static IObservable<IChangeSet<TObject, TKey>> Append<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
          TObject item, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.StartWith(new ChangeSet<TObject, TKey>(ChangeReason.Add, key, item));
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            CombineOperator type,
            params IObservable<IChangeSet<TObject, TKey>>[] combinetarget)
        {
            if (combinetarget == null) throw new ArgumentNullException("combinetarget");
            
            //TODO: Combine these collections using merge (with index)
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                        {
                            Action<IChangeSet<TObject, TKey>> updateAction = updates =>
                                {
                                    try
                                    {
                                        observer.OnNext(updates);
                                    }
                                    catch (Exception ex)
                                    {
                                        observer.OnError(ex);
                                        observer.OnCompleted();
                                    }
                                };
                            IDisposable subscriber = Disposable.Empty;
                            try
                            {
                                var list = combinetarget.ToList();
                                list.Insert(0, source);

                                var combiner = new Combiner<TObject, TKey>(type, updateAction);
                                subscriber = combiner.Subscribe(list.ToArray());
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                observer.OnCompleted();
                            }

                            return subscriber;
                        });
        }

        #endregion

        #region  Transform

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, TDestination> transformFactory)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (transformFactory == null) throw new ArgumentNullException("transformFactory");

            return Observable.Create<IChangeSet<TDestination, TKey>>
                (
                    observer =>
                    {
                        var transformer = new Transformer<TDestination, TSource, TKey>(null);
                        return source
                            .Select(updates => transformer.Transform(updates, transformFactory))
                            .NotEmpty()
                            .Finally(observer.OnCompleted)
                            .SubscribeSafer(observer);
                    });
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (transformFactory == null) throw new ArgumentNullException("transformFactory");

            return Observable.Create<IChangeSet<TDestination, TKey>>
                (
                    observer =>
                    {
                        var transformer = new Transformer<TDestination, TSource, TKey>( null);
                        return source
                            .Select(updates => transformer.Transform(updates, transformFactory))
                            .NotEmpty()
                            .SubscribeSafer(observer);
                    });
        }

        #endregion

        #region Transform many

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child. 
        /// 
        /// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>s
        /// <returns></returns>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany
              <TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source,
              Func<TSource, IEnumerable<TDestination>> manyselector)
            where TDestination : IKey<TDestinationKey>
        {
            return Observable.Create<IChangeSet<TDestination, TDestinationKey>>
            (
                observer =>
                {
                    var flattend = source.FlattenWithSingleParent(manyselector, t => t.Key);
                    var subscriber = flattend.SubscribeSafer(observer);

                    return Disposable.Create(() =>
                    {
                        observer.OnCompleted();
                        subscriber.Dispose();

                    });
                });
        }


        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child. 
        /// 
        /// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector which must be unique across all</param>
        /// <param name="childHasOneParent">if set to <c>true</c> the child only ever belongs to one parent</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany
            <TDestination, TDestinationKey, TSource, TSourceKey>(
            this IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IEnumerable<TDestination>> manyselector, Func<TDestination, TDestinationKey> keySelector,
            bool childHasOneParent = true)
        {
            return Observable.Create<IChangeSet<TDestination, TDestinationKey>>
                (
                    observer => source.FlattenWithSingleParent(manyselector, keySelector)
                                      .SubscribeSafer(observer));
        }

        /// <summary>
        /// Flattens the with single parent.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        private static IObservable<IChangeSet<TDestination, TDestinationKey>> FlattenWithSingleParent
              <TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source,
              Func<TSource, IEnumerable<TDestination>> manyselector, Func<TDestination, TDestinationKey> keySelector)
        {
            //TODO: Add error handling
            return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
                observer =>
                    {
                        var cache = new Cache<TDestination, TDestinationKey>();
                        var updater = new IntermediateUpdater<TDestination, TDestinationKey>(cache);

                        return source.Subscribe(updates =>
                            {
                                var children = updates.SelectMany(u =>
                                    {
                                        var many = manyselector(u.Current);
                                        return many.Select(m => new TransformedItem<TDestination>(u.Reason, m));
                                    });

                                foreach (var child in children)
                                {
                                    var key = keySelector(child.Current);
                                    switch (child.Reason)
                                    {
                                        case ChangeReason.Add:
                                        case ChangeReason.Update:
                                            updater.AddOrUpdate(child.Current, key);
                                            break;
                                        case ChangeReason.Remove:
                                            updater.Remove(key);
                                            break;
                                        case ChangeReason.Evaluate:
                                            updater.Evaluate( key);
                                            break;
                                    }
                                }

                                var changes = updater.AsChangeSet();
                                if (changes.Count != 0)
                                    observer.OnNext(changes);

                            });
                    }
                );
        }
        
        #endregion
        
        #region Transform safe


        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
        ///  If not specified the stream will terminate as per rx convention.
        /// </param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory,
             Action<Error<TSource, TKey>> errorHandler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (transformFactory == null) throw new ArgumentNullException("transformFactory");
            if (errorHandler == null) throw new ArgumentNullException("errorHandler");

            return Observable.Create<IChangeSet<TDestination, TKey>>
                (
                    observer =>
                    {
                        var transformer = new Transformer<TDestination, TSource, TKey>( errorHandler);
                        return source
                            .Select(updates => transformer.Transform(updates, transformFactory))
                            .NotEmpty()
                            .SubscribeSafer(observer);
                    });
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
        ///  If not specified the stream will terminate as per rx convention.
        /// </param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, TDestination> transformFactory,
            Action<Error<TSource, TKey>> errorHandler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (transformFactory == null) throw new ArgumentNullException("transformFactory");
            if (errorHandler == null) throw new ArgumentNullException("errorHandler");

            return Observable.Create<IChangeSet<TDestination, TKey>>
                (
                    observer =>
                    {
                        var transformer = new Transformer<TDestination, TSource, TKey>(errorHandler);
                        return source
                            .Select(updates => transformer.Transform(updates, transformFactory))
                            .NotEmpty()
                            .Finally(observer.OnCompleted)
                            .SubscribeSafer(observer);
                    });
        }

        #endregion

        #region Distinct values

        /// <summary>
        ///     Selects distinct values from the source.
        /// </summary>
        /// <typeparam name="TObject">The tyoe object from which the distinct values are selected</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The soure.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        /// <remarks>
        /// Due to it's nature only adds or removes can be returned
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(
            this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (valueSelector == null) throw new ArgumentNullException("valueSelector");

            return Observable.Create<IDistinctChangeSet<TValue>>
                (
                    observer =>
                    {
                        var distinctObserver = new DistinctCalculator<TObject, TKey, TValue>(valueSelector);
                            var subscriber = source
                                .Select(distinctObserver.Calculate)
                                .Where(updates=>updates.Count != 0)
                                .SubscribeSafer(observer);

                            return Disposable.Create(subscriber.Dispose);
                        }
                );
        }

        #endregion
        
        #region   Grouping

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        ///  A group is included for each item in the resulting group source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelector">The group selector factory.</param> 
        /// <param name="resultGroupSource">
        ///   A distinct stream used to determine the result
        /// </param>
        /// <remarks>
        /// Useful for parent-child collection when the parent and child are soured from different streams
        /// </remarks>
        /// <returns></returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(
                this IObservable<IChangeSet<TObject, TKey>> source, 
                Func<TObject, TGroupKey> groupSelector, 
                IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (groupSelector == null) throw new ArgumentNullException("groupSelector");
            if (resultGroupSource == null) throw new ArgumentNullException("resultGroupSource");


            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
                (
                    observer =>
                        {
                            var locker = new object();
                            
                            //create source group cache
                            var sourceGroups = source.Synchronize(locker)
                                    .Group(groupSelector)

                                    .DisposeMany()
                                    .AsObservableCache();

                            //create parent groups
                            var parentGroups = resultGroupSource.Synchronize(locker)
                                        .Transform(x =>
                                            {
                                                //if child already has data, populate it.
                                                var result = new ManagedGroup<TObject, TKey, TGroupKey>(x);
                                                var child = sourceGroups.Lookup(x);
                                                if (child.HasValue)
                                                {
                                                    //dodgy cast but fine as a groups is always a ManagedGroup;
                                                    var group = (ManagedGroup<TObject, TKey, TGroupKey>)child.Value;
                                                    result.Update(updater => updater.Update(group.GetInitialUpdates()));
                                                }
                                                return result;
                                            })
                                        .DisposeMany()
                                        .AsObservableCache();


                            //connect to each individual item and update the resulting group
                            var updateFromcChilds = sourceGroups.Connect()
                                    .SubscribeMany(x => x.Cache.Connect().Subscribe(updates =>
                                    {
                                        var groupToUpdate = parentGroups.Lookup(x.Key);
                                        if (groupToUpdate.HasValue)
                                        {                
                                            groupToUpdate.Value.Update(updater => updater.Update(updates));
                                        }
                                    }))
                                    .DisposeMany()
                                    .Subscribe();

                            var notifier = parentGroups
                                    .Connect()
                                    .Select(x =>
                                    {
                                        var groups = x.Select(s => new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(s.Reason, s.Key, s.Current));
                                        return new GroupChangeSet<TObject, TKey, TGroupKey>(groups);
                                    })
                                .SubscribeSafer(observer);

                            return Disposable.Create(() =>
                                                         {
                                                             notifier.Dispose();
                                                             sourceGroups.Dispose();
                                                             parentGroups.Dispose();
                                                             updateFromcChilds.Dispose();
                                                         });
                        });
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <returns></returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)

     {
            if (source == null) throw new ArgumentNullException("source");
            if (groupSelectorKey == null) throw new ArgumentNullException("groupSelectorKey");

            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
                (
                    observer =>
                        {
                            var grouper = new Grouper<TObject, TKey, TGroupKey>(groupSelectorKey);

                            var  groups = source.Select(grouper.Update)
                                .Where(changes=>changes.Count!=0).Publish();

                            var subscriber = groups.SubscribeSafer(observer);
                            var disposer = groups.DisposeMany().Subscribe();
                           
                            var connected = groups.Connect();

                            return Disposable.Create(() =>
                            {
                                connected.Dispose();
                                disposer.Dispose();
                                subscriber.Dispose();
                            });
                        });
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="groupController">The group controller which enables reapplying the group</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController
        /// </exception>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, 
            Func<TObject, TGroupKey> groupSelectorKey, GroupController groupController)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (groupSelectorKey == null) throw new ArgumentNullException("groupSelectorKey");
            if (groupController == null) throw new ArgumentNullException("groupController");

            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
                (
                    observer =>
                    {
                        var locker = new object();
                        var grouper = new Grouper<TObject, TKey, TGroupKey>(groupSelectorKey);

                        var groups = source
                            .Synchronize(locker)
                            .Select(grouper.Update)
                            .Where(changes => changes.Count != 0);

                        var regroup = groupController.Regrouped
                            .Synchronize(locker)
                            .Select(_ => grouper.Regroup())
                            .Where(changes => changes.Count != 0);

                        var published = groups.Merge(regroup).Publish();
                        var subscriber = published.SubscribeSafe(observer);
                        var disposer = published.DisposeMany().Subscribe();
                        
                        var connected = published.Connect();

                        return Disposable.Create(() =>
                        {
                            connected.Dispose();
                            disposer.Dispose();
                            subscriber.Dispose();
                        });
                    });
        }


        #endregion

        #region Virtualisation

        /// <summary>
        /// Limits the size of the result set to the specified number
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source, int size)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (size <= 0) throw new ArgumentOutOfRangeException("size","Size should be greater than zero");

            return Observable.Create<IVirtualChangeSet<TObject, TKey>>
            (
                observer =>
                {
                    var virtualiser = new Virtualiser<TObject, TKey>(new VirtualRequest(0, size));

                    return source.FinallySafe(observer.OnCompleted)
                        .Select(virtualiser.Update)
                        .Where(updates => updates != null)
                        .FinallySafe(observer.OnCompleted)
                        .SubscribeSafer(observer);
                });
        }

        /// <summary>
        /// Virtualises the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="virtualisingController">The virtualising controller.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
            VirtualisingController virtualisingController)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (virtualisingController == null) throw new ArgumentNullException("virtualisingController");

            return Observable.Create<IVirtualChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var virtualiser = new Virtualiser<TObject, TKey>();
                        var locker = new object();

                        var request = virtualisingController
                            .Changed.Synchronize(locker)
                            .Select(virtualiser.Virtualise);
                        
                        var datachange = source.Synchronize(locker)
                            .Select(virtualiser.Update);

                        return request.Merge(datachange)
                                            .Where(updates=>updates!=null)
                                            .FinallySafe(observer.OnCompleted)
                                            .SubscribeSafer(observer);
                    });
        }

        #endregion
        
        #region Binding

        /// <summary>
        ///  Binds the results to the specified observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                              IObservableCollection<TObject> destination)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");
            var updater = new ObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }


        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservableCollection<TObject> destination,
            IObservableCollectionAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");
            if (updater == null) throw new ArgumentNullException("updater");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (observer =>
                {
                    var locker = new object();
                    var published = source.Synchronize(locker).Publish();

                    var adaptor = published
                        .Subscribe(updates =>
                        {
                            try
                            {
                                updater.Adapt(updates, destination);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                observer.OnCompleted();
                            }
                        },
                        observer.OnError, observer.OnCompleted);

                    var connected = published.Connect();

                    var subscriber = published.SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                        adaptor.Dispose();
                        subscriber.Dispose();
                        connected.Dispose();
                        subscriber.Dispose();
                    });
                }
                );
        }


        /// <summary>
        ///  Binds the results to the specified observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                      IObservableCollection<TObject> destination)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");
            var updater = new SortedObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }


        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source,
            IObservableCollection<TObject> destination,
            ISortedObservableCollectionAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");
            if (updater == null) throw new ArgumentNullException("updater");

            return Observable.Create<ISortedChangeSet<TObject, TKey>>
                (observer =>
                {
                    var locker = new object();
                    var published = source.Synchronize(locker).Publish();

                    var adaptor = published
                        .Subscribe(updates =>
                        {
                            try
                            {
                                updater.Adapt(updates, destination);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex); 
                                observer.OnCompleted();
                            }
                        },
                        observer.OnError, observer.OnCompleted);

                    var connected = published.Connect();
                    var subscriber = published.SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                        adaptor.Dispose();
                        subscriber.Dispose();
                        connected.Dispose();
                        subscriber.Dispose();
                    });
                }
                );
        }


        #endregion

        #region Adaptor

        /// <summary>
        /// Inject side effects into the stream using the specified adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,IChangeSetAdaptor<TObject, TKey> adaptor)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (adaptor == null) throw new ArgumentNullException("adaptor");

            return source.Do(adaptor.Adapt);
        }

        /// <summary>
        /// Inject side effects into the stream using the specified sorted adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (adaptor == null) throw new ArgumentNullException("adaptor");

            return source.Do(adaptor.Adapt);
        }
        #endregion
    }
}