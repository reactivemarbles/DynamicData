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
	/// Extenstions for ObservableList
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


		/// <summary>
		/// Filters the source using the specified transformFactory
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="predicate">The transformFactory.</param>
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
		/// transformFactory
		/// </exception>
		public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (transformFactory == null) throw new ArgumentNullException("transformFactory");
			var filter = new Transformer<TSource,TDestination>(transformFactory);
			return source.Select(filter.Process).NotEmpty();
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


		#region Buffering


		/// <summary>
		/// Batches changesets for the spefied duration
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="timeSpan">The time span.</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> Batch<T>(this IObservable<IChangeSet<T>> source,
															TimeSpan timeSpan,
															IScheduler scheduler = null)
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Buffer(timeSpan, scheduler ?? Scheduler.Default).ToChangeSet();
		}

		/// <summary>
		/// Convert the result of a buffer operation to a change set
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IChangeSet<T>> ToChangeSet<T>(this IObservable<IList<IChangeSet<T>>> source)
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
