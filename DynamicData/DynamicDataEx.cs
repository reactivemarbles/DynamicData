
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Annotations;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Internal;
using DynamicData.Kernel;
using DynamicData.Operators;


namespace DynamicData
{
	
    /// <summary>
    /// Extensions for dynamic data
    /// </summary>
    public static class DynamicDataEx
    {

		#region General


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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (finallyAction == null) throw new ArgumentNullException(nameof(finallyAction));

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
					() => {
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
	
        /// <summary>
		/// Cache equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the destination key.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<TObject, TKey>> PublishRefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

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
        /// Monitors the status of a stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T>  source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

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
                                .SubscribeSafe(observer);

                    return Disposable.Create(() =>
                        {
                            statusSubject.OnCompleted();
                            monitor.Dispose();
                            subscriber.Dispose();
                        });
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
            if (source == null) throw new ArgumentNullException(nameof(source));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
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
                        return true;

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (includeFunction == null) throw new ArgumentNullException(nameof(includeFunction));
 
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
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return Observable.Create<ItemWithValue<TObject, TDestination>>
                (
                    observer => source.SubscribeMany(t => observableSelector(t)
                        .Select(value => new ItemWithValue<TObject, TDestination>(t, value))
                        .SubscribeSafe(observer))
                        .Subscribe()

                );
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
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject,TKey, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return Observable.Create<ItemWithValue<TObject, TDestination>>
                (
                    observer => source.SubscribeMany((t, v) => observableSelector(t, v)
                        .Select(z => new ItemWithValue<TObject, TDestination>(t, z))
                        .SubscribeSafe(observer))
                        .Subscribe()
                );
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return Observable.Create<TDestination>
                (
                    observer => source.SubscribeMany(t => observableSelector(t)
                        .SubscribeSafe(observer))
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return Observable.Create<TDestination>
                (
                    observer => source.SubscribeMany((t,v) => observableSelector(t,v).SubscribeSafe(observer))
                        .Subscribe());
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TValue> WhenAnyValueChanged<TObject, TKey, TValue>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.MergeMany(t => t.WhenValueChanged(propertyAccessor, notifyOnInitialValue));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<PropertyValue<TObject, TValue>> WhenAnyPropertyChanged<TObject, TKey, TValue>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.MergeMany(t => t.WhenChanged(propertyAccessor, notifyOnInitialValue));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
						var disposer = new OnBeingRemoved<TObject, TKey>(removeAction);
						var subscriber = source
                            .Do(disposer.RegisterForRemoval, observer.OnError)
                            .SubscribeSafe(observer);

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
        /// Individual items are disposed when removed or replaced. All items
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
	                                        d?.Dispose();
                                        });
        }


        /// <summary>
        /// Includes changes for the specified reasons only
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
             if (reasons == null) throw new ArgumentNullException(nameof(reasons));
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
             if (reasons == null) throw new ArgumentNullException(nameof(reasons));
             if (!reasons.Any()) throw new ArgumentException("Must select at least one reason");

             var hashed = new HashSet<ChangeReason>(reasons);

             return source.Select(updates =>
             {
                 var filtered = updates.Where(u => !hashed.Contains(u.Reason));
                 return new ChangeSet<TObject, TKey>(filtered);
             }).NotEmpty();
         }

        #endregion

        #region Conversion


        /// <summary>
        /// Changes the primary key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source,
            Func<TObject, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return source.Select(updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
        }


        /// <summary>
        /// Convert the object using the sepcified conversion function.
        /// This is a lighter equivalent of Transform and is designed to be used with non-disposable objects
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Convert<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));

            return source.Select(changes =>
            {
                 var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                    change.Key,
                    conversionFactory(change.Current),
                    change.Previous.Convert(conversionFactory),
                    change.CurrentIndex,
                    change.PreviousIndex));

                return new ChangeSet<TDestination, TKey>(transformed);
            });
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
            if (source == null) throw new ArgumentNullException(nameof(source));

	        return source.Buffer(timeSpan, scheduler ?? Scheduler.Default).FlattenBufferResult();

        }

		/// <summary>
		/// Convert the result of a buffer operation to a single change set
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<TObject, TKey>> FlattenBufferResult<TObject, TKey>([NotNull] this IObservable<IList<IChangeSet<TObject, TKey>>> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return source.Where(x => x.Count != 0)
					.Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));
		}


		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<bool> pauseIfTrueSelector,    
            IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, false, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<bool> pauseIfTrueSelector,
            bool intialPauseState = false,    
            IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, intialPauseState, null, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<bool> pauseIfTrueSelector,
            TimeSpan? timeOut= null,    
            IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                    IObservable<bool> pauseIfTrueSelector,
                                                    bool intialPauseState = false,
                                                    TimeSpan? timeOut= null,    
                                                    IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            ;

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        bool paused = intialPauseState;
                        var locker = new object();
                        var buffer = new List<Change<TObject, TKey>>();
                        var timeoutSubscriber = new SerialDisposable();
                        var timeoutSubject = new Subject<bool>();

                        var schedulertouse = scheduler ?? Scheduler.Default;

                        var bufferSelector = Observable.Return(intialPauseState)
                                                .Concat(pauseIfTrueSelector.Merge(timeoutSubject))
                                                .ObserveOn(schedulertouse)
                                                .Synchronize(locker)
                                                .Publish();
                        
                        var pause = bufferSelector.Where(state => state)
                                        .Subscribe(_ =>
                                        {
                                            paused = true;
                                           //add pause timeout if required
                                            if (timeOut != null && timeOut.Value != TimeSpan.Zero)
                                                timeoutSubscriber.Disposable = Observable.Timer(timeOut.Value, schedulertouse)
                                                                                .Select(l => false)
                                                                                .SubscribeSafe(timeoutSubject);
                                        });


                        var resume = bufferSelector.Where(state => !state)
                                    .Subscribe(_ =>
                                        {
                                            paused = false;
                                            //publish changes and clear buffer
                                            if (buffer.Count==0) return;
                                            observer.OnNext(new ChangeSet<TObject, TKey>(buffer));
                                            buffer.Clear();

                                            //kill off timeout if required
                                            timeoutSubscriber.Disposable = Disposable.Empty;
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
            if (source == null) throw new ArgumentNullException(nameof(source));
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
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.MonitorStatus()
                .Where(status => status == ConnectionStatus.Loaded)
                .Take(1)
                .Select(_ => new ChangeSet<TObject, TKey>())
                .Concat(source)
                .NotEmpty();
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.CountChanged.Where(count => count != 0)
                    .Take(1)
                    .Select(_ => new ChangeSet<TObject, TKey>())
                    .Concat(source.Connect())
                    .NotEmpty();
        }

        #endregion

        #region True for all values



        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// 
        /// i) The cache changes
        /// or ii) The inner observable changes
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, IObservable<TValue>> observableSelector,
            Func<TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }



        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// 
        /// i) The cache changes
        /// or ii) The inner observable changes
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, IObservable<TValue>> observableSelector,
                Func<TObject, TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, IObservable<TValue>> observableSelector,
        Func<TObject, TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }




        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, IObservable<TValue>> observableSelector,
                Func<TValue, bool> equalityCondition)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));
            if (equalityCondition == null) throw new ArgumentNullException(nameof(equalityCondition));

            return source.TrueFor(observableSelector,
                items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }

        private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                        Func<TObject, IObservable<TValue>> observableSelector,
                        Func<IEnumerable<ObservableWithValue<TObject,TValue>>, bool> collectionMatcher)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Observable.Create<bool>(observer =>
            {
                var transformed = source.Transform(t => new ObservableWithValue<TObject, TValue>(t,observableSelector(t))).Publish();
                var inlineChanges = transformed.MergeMany(t => t.Observable);
                var queried = transformed.QueryWhenChanged(q => q.Items);

                //nb: we do not care about the inline change because we are only monitoring it to cause a re-evalutaion of all items
                var publisher = queried.CombineLatest(inlineChanges, (items, inline) => collectionMatcher(items))
                 .DistinctUntilChanged()
                 .SubscribeSafe(observer);

                return new CompositeDisposable( publisher, transformed.Connect());
            });
        }
        #endregion
        
        #region Entire Collection Operators



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
        public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, 
            Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return source.QueryWhenChanged().Select(resultSelector);
        }



        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var cache = new Cache<TObject, TKey>();
            var query = new AnomynousQuery<TObject, TKey>(cache);

            return source.Do(changes=> cache.Clone(changes)).Select(changes => query);
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) on subscription
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemChangedTrigger">Should the query be triggers for observables on individual items</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var locker = new object();
            var cache = new Cache<TObject, TKey>();
            var query = new AnomynousQuery<TObject, TKey>(cache);
            
            return source.Publish(shared =>
            {
                var inlineChange = shared.MergeMany(itemChangedTrigger)
                            .Synchronize(locker)   
                            .Select(_ => query);

                var sourceChanged = shared
                            .Synchronize(locker)
                            .Do(changes => cache.Clone(changes))
                            .Select(changes => query);

                return sourceChanged.Merge(inlineChange);
            });
        }

        /// <summary>
        /// Converts the changeset into a fully formed collection. Each change in the source results in a new collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items, query.Count));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Watch(key).Select(u => u.Current);
        }
		/// <summary>
		/// Returns an observable of any updates which match the specified key,  preceeded with the initital cache state
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
		}

		#endregion

		#region Clone

		internal static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ICache<TObject,TKey> cache)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            
            return Observable.Create<IChangeSet<TObject, TKey>>
                                (
                                    observer => source
                                        .Do(cache.Clone)
                                        .NotEmpty()
                                        .SubscribeSafe(observer));
                        }

        internal static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var cache = new Cache<TObject, TKey>();
            return source.Do(cache.Clone);
        }

        internal static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IDictionary<TKey, TObject> target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            return source.Do(changes =>
            {
                foreach (var item in changes)
                {
                    switch (item.Reason)
                    {
                        case ChangeReason.Update:
                        case ChangeReason.Add:
                            {
                                target[item.Key] = item.Current;
                            }
                            break;
                        case ChangeReason.Remove:
                            target.Remove(item.Key);
                            break;
                    }
                }
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
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
               Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            return source.ExpireAfter(timeSelector, null, scheduler);
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
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval)
        {
            return ExpireAfter(source, timeSelector, pollingInterval, null);
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
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
              //  var dateTime = DateTime.Now;
                scheduler = scheduler ?? Scheduler.Default;
                var cache = new IntermediateCache<TObject, TKey>(source);
                
                  var published = cache.Connect().Publish();
                var subscriber = published.SubscribeSafe(observer);


                var autoRemover = published.ForExpiry(timeSelector, pollingInterval, scheduler)
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
        internal static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> ForExpiry<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentException("Size limit must be greater than zero");

                return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                {
                    var sizeLimiter = new SizeLimiter<TObject, TKey>(size);

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
                                            .SubscribeSafe(observer);
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
                        .SubscribeSafe(observer);
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
                                .SubscribeSafe(observer);
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
                                .SubscribeSafe(observer);
                    });
        }


        #endregion

        #region   And, or, except



        /// <summary>
        /// Applied a logical And operator between the collections i.e items which are in all of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.ContainedInEach, others);
        }

        /// <summary>
        /// Applied a logical Or operator between the collections i.e items which are in any of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.ContainedInAny, others);
        }


        /// <summary>
        /// Applied a logical Intersect operator between the collections i.e items from the first set are included unless contained in the other
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.ExceptFor, others);
        }


        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            CombineOperator type,
            params IObservable<IChangeSet<TObject, TKey>>[] combinetarget)
        {
            if (combinetarget == null) throw new ArgumentNullException(nameof(combinetarget));

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

		/// <summary>
		/// The equivalent of rx startwith operator, but wraps the item in a change where reason is ChangeReason.Add
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
				  TObject item) where TObject : IKey<TKey>
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return source.StartWithItem(item, item.Key);
		}

		/// <summary>
		/// The equivalent of rx startwith operator, but wraps the item in a change where reason is ChangeReason.Add
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
				  TObject item, TKey key)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return source.StartWith(new ChangeSet<TObject, TKey>(ChangeReason.Add, key, item));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            var transformer = new Transformer<TDestination, TSource, TKey>(null);
		    return source
		        .Select(updates => transformer.Transform(updates, transformFactory))
		        .NotEmpty();
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            var transformer = new Transformer<TDestination, TSource, TKey>(null);
            return source
                .Select(updates => transformer.Transform(updates, transformFactory))
                .NotEmpty();
        }


        /// <summary>
        /// Transforms the object to a fully recursive tree, create a hiearchy based on the pivot function
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pivotOn">The pivot on.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, 
            [NotNull] Func<TObject, TKey> pivotOn)
                   where TObject : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pivotOn == null) throw new ArgumentNullException(nameof(pivotOn));
            return new TreeBuilder<TObject, TKey>(source, pivotOn).Run();
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
            return source.FlattenWithSingleParent(manyselector, t => t.Key);
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
            return source.FlattenWithSingleParent(manyselector, keySelector);
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            var transformer = new Transformer<TDestination, TSource, TKey>(errorHandler);
            return source.Select(updates => transformer.Transform(updates, transformFactory)).NotEmpty();
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            var transformer = new Transformer<TDestination, TSource, TKey>(errorHandler);
            return source.Select(updates => transformer.Transform(updates, transformFactory)).NotEmpty();
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
        public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return new DistinctCalculator<TObject, TKey, TValue>(source,valueSelector).Run();
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
            if (resultGroupSource == null) throw new ArgumentNullException(nameof(resultGroupSource));


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
                                .SubscribeSafe(observer);

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));

            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
                (
                    observer =>
                        {
                            var grouper = new Grouper<TObject, TKey, TGroupKey>(groupSelectorKey);

                            var groups = source.Select(grouper.Update)
                                .Where(changes => changes.Count != 0).Publish();

                            var subscriber = groups.SubscribeSafe(observer);
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));
            if (groupController == null) throw new ArgumentNullException(nameof(groupController));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size),"Size should be greater than zero");
            var virtualiser = new Virtualiser<TObject, TKey>(new VirtualRequest(0, size));

            return source.Select(virtualiser.Update).Where(changes => changes != null);
        }

        /// <summary>
        /// Limits the size of the result set to the specified number, ordering by the comparer
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero</exception>
    
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source, 
            IComparer<TObject> comparer,
            int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");

            return source.Sort(comparer).Top(size);

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (virtualisingController == null) throw new ArgumentNullException(nameof(virtualisingController));

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
                                            .SubscribeSafe(observer);
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (updater == null) throw new ArgumentNullException(nameof(updater));
            return source.Do(changes => updater.Adapt(changes, destination));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            return source.Do(changes => updater.Adapt(changes, destination));
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));

            return source.Do(adaptor.Adapt);
        }
        #endregion
    }
}