using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Extenssions for ObservableList
	/// </summary>
	public static class ObservableListEx
	{
		/// <summary>
		/// Converts the source list to an read only observable list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return new AnomynousObservableList<T>(source);
		}


		/// <summary>
		/// Converts the source observable to an read only observable list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservableList<T> AsObservableList<T>(this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return new AnomynousObservableList<T>(source);
		}

		#region Core List Operators



		/// <summary>
		/// Filters the source using the specified valueSelector
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="predicate">The valueSelector.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (predicate == null) throw new ArgumentNullException("predicate");
			var filter = new ImmutableFilter<T>(predicate);
			return source.Select(filter.Process).NotEmpty();
		}

		/// <summary>
		/// Sorts the sequence using the specified comparer.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="comparer">The comparer.</param>
		/// <param name="options">The options.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// comparer
		/// </exception>
		public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IComparer<T> comparer, SortOptions options=SortOptions.None)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (comparer == null) throw new ArgumentNullException("comparer");
			var sorter = new Sorter<T>(comparer, options);
			return source.Select(sorter.Process).NotEmpty();
		}


		/// <summary>
		/// Projects each update item to a new form using the specified transform function
		/// </summary>
		/// <typeparam name="TSource">The type of the source.</typeparam>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="transformFactory">The transform factory.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// valueSelector
		/// </exception>
		public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (transformFactory == null) throw new ArgumentNullException("transformFactory");
			var transformer = new Transformer<TSource,TDestination>(transformFactory);
			return source.Select(transformer.Process).NotEmpty();
		}

		/// <summary>
		/// Selects distinct values from the source, using the specified value selector
		/// </summary>
		/// <typeparam name="TObject">The type of the source.</typeparam>
		/// <typeparam name="TValue">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="valueSelector">The transform factory.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// valueSelector
		/// </exception>
		public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, 
			Func<TObject, TValue> valueSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (valueSelector == null) throw new ArgumentNullException("valueSelector");
			var calculator = new DistinctCalculator<TObject, TValue>();
			return source.Transform(t=>new ItemWithValue<TObject, TValue>(t, valueSelector(t)))
				.Select(calculator.Process)
				.NotEmpty();
		}

		/// <summary>
		///  Groups the source on the value returned by group selector factory. 
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TGroup">The type of the group.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="groupSelector">The group selector.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// groupSelector
		/// </exception>
		public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOn<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroup> groupSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (groupSelector == null) throw new ArgumentNullException("groupSelector");
			var calculator = new Grouper<TObject, TGroup>();

			return source.Transform(t => new ItemWithValue<TObject, TGroup>(t, groupSelector(t)))
				.Select(calculator.Process)
				.DisposeMany() //dispose removes as the grouping is disposable
				.NotEmpty();
		}

		/// <summary>
		/// Prevents an empty notification
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> NotEmpty<T>(this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return source.Where(s => s.Count != 0);
		}

		/// <summary>
		/// Clones the target list as a side effect of the stream
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="target"></param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
		{
			if (source == null) throw new ArgumentNullException("source");
			return source.Do(target.Clone);
		}

		#endregion

		#region Item operators

		/// <summary>
		/// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
		/// when it is no longer part of the stream.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="observableSelector">The observable selector.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source
		/// or
		/// observableSelector</exception>
		public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (observableSelector == null) throw new ArgumentNullException("observableSelector");

			return Observable.Create<TDestination>
				(
					observer => source.SubscribeMany(t=> observableSelector(t).SubscribeSafe(observer))
						.Subscribe());
		}

		/// <summary>
		/// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="subscriptionFactory">The subsription function</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source
		/// or
		/// subscriptionFactory</exception>
		/// <remarks>
		/// Subscribes to each item when it is added or updates and unsubcribes when it is removed
		/// </remarks>
		public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (subscriptionFactory == null) throw new ArgumentNullException("subscriptionFactory");

			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						var published = source.Publish();
						var subscriptions = published
											.Transform(t => subscriptionFactory)
											.DisposeMany()
											.Subscribe();

						var result = published.SubscribeSafe(observer);
						var connected = published.Connect();

						return new CompositeDisposable(subscriptions, connected, result);
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
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns>A continuation of the original stream</returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
		{
			return source.OnItemRemoved(t =>
			{
				var d = t as IDisposable;
				d?.Dispose();
			});
		}



		/// <summary>
		/// Callback for each item as and when it is being removed from the stream
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="removeAction">The remove action.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// removeAction
		/// </exception>
		public static IObservable<IChangeSet<T>> OnItemRemoved<T>(this IObservable<IChangeSet<T>> source, Action<T> removeAction)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (removeAction == null) throw new ArgumentNullException("removeAction");
			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						var disposer = new OnBeingRemoved<T>(removeAction);
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

		#endregion

		#region Reason filtering



		/// <summary>
		/// Includes changes for the specified reasons only
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="reasons">The reasons.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Must enter at least 1 reason</exception>
		public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ChangeReason[] reasons)
		{
			var matches = reasons.ToHashSet();
			if (matches.Count==0)
				throw new ArgumentException("Must enter at least 1 reason");

			return source.Select(updates =>
			{
				var filtered = updates.Where(u => matches.Contains(u.Reason));
				return new ChangeSet<T>(filtered);
			}).NotEmpty();
		}


		/// <summary>
		/// Excludes updates for the specified reasons
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="reasons">The reasons.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Must enter at least 1 reason</exception>
		public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ChangeReason[] reasons)
		{
			var matches = reasons.ToHashSet();
			if (matches.Count == 0)
				throw new ArgumentException("Must enter at least 1 reason");

			return source.Select(updates =>
			{
				var filtered = updates.Where(u => !matches.Contains(u.Reason));
				return new ChangeSet<T>(filtered);
			}).NotEmpty();
		}
		#endregion

		#region Buffering


		/// <summary>
		/// Convert the result of a buffer operation to a change set
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
		{
			return source
					.Where(x => x.Count != 0)
					.Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));

		}

		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<TObject>> BufferIf<TObject>(this IObservable<IChangeSet<TObject>> source,
			IObservable<bool> pauseIfTrueSelector,
			IScheduler scheduler = null)
		{
			return BufferIf(source, pauseIfTrueSelector, false, scheduler);
		}

		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<TObject>> BufferIf<TObject>(this IObservable<IChangeSet<TObject>> source,
			IObservable<bool> pauseIfTrueSelector,
			bool intialPauseState = false,
			IScheduler scheduler = null)
		{
			return BufferIf(source, pauseIfTrueSelector, intialPauseState, null, scheduler);
		}

		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<TObject>> BufferIf<TObject>(this IObservable<IChangeSet<TObject>> source,
			IObservable<bool> pauseIfTrueSelector,
			TimeSpan? timeOut = null,
			IScheduler scheduler = null)
		{
			return BufferIf(source, pauseIfTrueSelector, false, timeOut, scheduler);
		}


		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
		/// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
													IObservable<bool> pauseIfTrueSelector,
													bool intialPauseState = false,
													TimeSpan? timeOut = null,
													IScheduler scheduler = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (pauseIfTrueSelector == null) throw new ArgumentNullException("pauseIfTrueSelector");
			;

			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						bool paused = intialPauseState;
						var locker = new object();
						var buffer = new List<Change<T>>();
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
										if (buffer.Count == 0) return;
										observer.OnNext(new ChangeSet<T>(buffer));
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
		/// Defer the subscription until the cache has been inflated with data
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.CountChanged.Where(count => count != 0)
							.Take(1)
							.Select(_ => new ChangeSet<T>())
							.Concat(source.Connect())
							.NotEmpty();
		}

		/// <summary>
		/// Defer the subscribtion until loaded and skip initial changeset
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return source.DeferUntilLoaded().Skip(1);
		}



		/// <summary>
		/// Defer the subscription until the stream has been inflated with data
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						var published = source.Publish();

						var subscriber = published.MonitorStatus()
											 .Where(status => status == ConnectionStatus.Loaded)
											 .Take(1)
											 .Select(_ => new ChangeSet<T>())
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
	}
}
