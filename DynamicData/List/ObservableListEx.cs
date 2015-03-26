using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Controllers;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Extenssions for ObservableList
	/// </summary>
	public static class ObservableListEx
	{

		#region Populate into an observable cache


		/// <summary>
		/// list.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="destination">The destination.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException">
		/// source
		/// or
		/// destination
		/// </exception>
		/// <exception cref="System.ArgumentNullException">source
		/// or
		/// destination</exception>
		public static IDisposable PopulateInto<T>([NotNull] this IObservable<IChangeSet<T>> source,[NotNull] ISourceList<T> destination)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (destination == null) throw new ArgumentNullException("destination");

			return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
		}


		#endregion

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
			return new ImmutableFilter<T>(source, predicate).Run();
		}

		/// <summary>
		/// Filters the specified filter controller.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="filterController">The filter controller.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// filterController
		/// </exception>
		public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, FilterController<T> filterController)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (filterController == null) throw new ArgumentNullException("filterController");
			return new MutableFilter<T>(source, filterController).Run();
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
			return new Sorter<T>(source, comparer, options).Run();
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
			return new Transformer<TSource, TDestination>(source,transformFactory).Run();
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
			return new Distinct<TObject, TValue>(source, valueSelector).Run();
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
			return new GroupOn<TObject, TGroup>(source, groupSelector).Run();
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
		public static IObservable<TDestination> MergeMany<T, TDestination>([NotNull] this IObservable<IChangeSet<T>> source,
			[NotNull] Func<T, IObservable<TDestination>> observableSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (observableSelector == null) throw new ArgumentNullException("observableSelector");

			return new MergeMany<T, TDestination>(source, observableSelector).Run();
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
			return new SubscribeMany<T>(source, subscriptionFactory).Run();
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

			return new OnBeingRemoved<T>(source, removeAction).Run();
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
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
			IObservable<bool> pauseIfTrueSelector,
			IScheduler scheduler = null)
		{
			return BufferIf(source, pauseIfTrueSelector, false, scheduler);
		}

		/// <summary>
		/// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
		/// When a resume signal has been received the batched updates will  be fired.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
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
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
		/// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
		/// <param name="scheduler">The scheduler.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
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
			return new BufferIf<T>(source, pauseIfTrueSelector, intialPauseState, timeOut, scheduler).Run();
		}



		/// <summary>
		///  The latest copy of the cache is exposed for querying after each modification to the underlying data
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="resultSelector">The result selector.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// resultSelector
		/// </exception>
		public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source,
			Func<IList<TObject>, TDestination> resultSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (resultSelector == null) throw new ArgumentNullException("resultSelector");

			return source.QueryWhenChanged().Select(resultSelector);
		}

		/// <summary>
		/// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IList<T>> QueryWhenChanged<T>([NotNull] this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return new QueryWhenChanged<T>(source).Run();
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
		public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>([NotNull] this IObservable<IChangeSet<T>> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			return new DeferUntilLoaded<T>(source).Run();
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

		#endregion
	}
}
