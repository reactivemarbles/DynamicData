using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Binding;
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
        #region Conversion

        /// <summary>
        /// Adds a key to each item,  which enables all caching features of dynamic data
        /// </summary>
        /// <typeparam name="TObject">The type of  object.</typeparam>
        /// <typeparam name="TKey">The type of  key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> WithKey<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject,TKey> keySelector )
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            return source.Select(changes =>
            {
                var enumerator = new ListChangeToKeyedChangeEnumerator<TObject, TKey>(changes, keySelector);
                return new ChangeSet<TObject, TKey>(enumerator);
            });
        }


        /// <summary>
        /// Convert the object using the sepcified conversion function.
        /// 
        /// This is a lighter equivalent of Transform and is designed to be used with non-disposable objects
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>([NotNull] this IObservable<IChangeSet<TObject>> source, 
            [NotNull] Func<TObject, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));
            return source.Select(changes => changes.Transform(conversionFactory));
        }


        #endregion

        #region Binding

        /// <summary>
        /// Binds a clone of the observable changeset to the target observable collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="targetCollection">The target collection.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// targetCollection
        /// </exception>
        public static IObservable<IChangeSet<T>> Bind<T>([NotNull] this IObservable<IChangeSet<T>> source,
			[NotNull] IObservableCollection<T> targetCollection, int resetThreshold=25 )
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (targetCollection == null) throw new ArgumentNullException(nameof(targetCollection));

			var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, resetThreshold);
			return source.Adapt(adaptor);
		}

		/// <summary>
		/// Injects a side effect into a changeset observable
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="adaptor">The adaptor.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// adaptor
		/// </exception>
		public static IObservable<IChangeSet<T>> Adapt<T>([NotNull] this IObservable<IChangeSet<T>> source,[NotNull] IChangeSetAdaptor<T> adaptor)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));
			return source.Do(adaptor.Adapt);
		}

		#endregion

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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (destination == null) throw new ArgumentNullException(nameof(destination));

			return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
		}


		/// <summary>
		/// Converts the source list to an read only observable list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			return new AnomynousObservableList<T>(source);
		}


		#endregion

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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (predicate == null) throw new ArgumentNullException(nameof(predicate));
			return new ImmutableFilter<T>(source, predicate).Run();
		}

        /// <summary>
        /// Filters the specified filter controller.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The filter controller.</param>
        /// <param name="filterPolicy">The filter policy.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// filterController</exception>
        public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, FilterController<T> filterController, FilterPolicy filterPolicy= FilterPolicy.ClearAndReplace)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (filterController == null) throw new ArgumentNullException(nameof(filterController));
			return new MutableFilter<T>(source, filterController, filterPolicy).Run();
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (comparer == null) throw new ArgumentNullException(nameof(comparer));
			return new Sort<T>(source, comparer, options).Run();
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
			return new Transformer<TSource, TDestination>(source,transformFactory).Run();
		}

		/// <summary>
		/// Equivalent to a select many transform. To work, the key must individually identify each child.
		/// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
		/// </summary>
		/// <typeparam name="TDestination">The type of the destination.</typeparam>
		/// <typeparam name="TSource">The type of the source.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="manyselector">The manyselector.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// manyselector
		/// </exception>
		public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination,  TSource>([NotNull] this IObservable<IChangeSet<TSource>> source,
			[NotNull] Func<TSource, IEnumerable<TDestination>> manyselector)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (manyselector == null) throw new ArgumentNullException(nameof(manyselector));
			return new TransformMany<TSource, TDestination>(source, manyselector).Run();
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));

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
		public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
		{
            if (reasons.Length == 0)
                throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));

            var matches = reasons.ToHashSet();
			return source.Select(updates =>
			{
				var filtered = updates.Where(u => matches.Contains(u.Reason)).YieldWithoutIndex(); ;
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
		public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
		{
            if (reasons.Length == 0)
                throw new ArgumentException("Must enter at least 1 reason",nameof(reasons));

            var matches = reasons.ToHashSet();
            return source.Select(updates =>
			{
				var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

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
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));
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
			if (source == null) throw new ArgumentNullException(nameof(source));

			return source.CountChanged.Where(count => count != 0)
							.Take(1)
							.Select(_ => new ChangeSet<T>())
							.Concat(source.Connect())
							.NotEmpty();
		}

        #endregion

        #region Virtualisation / Paging


        /// <summary>
        /// Virtualises the source using parameters provided by the specified virtualising controller
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="virtualisingController">The virtualising controller.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Virtualise<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] VirtualisingController virtualisingController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (virtualisingController == null) throw new ArgumentNullException(nameof(virtualisingController));
            return new Virtualiser<T>(source, virtualisingController).Run();
        }

        /// <summary>
        /// Limits the size of the result set to the specified number of items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="numberOfItems">The number of items.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Top<T>([NotNull] this IObservable<IChangeSet<T>> source, int numberOfItems)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (numberOfItems <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var controller = new VirtualisingController(new VirtualRequest(0, numberOfItems));
                var subscriber = source.Virtualise(controller).SubscribeSafe(observer);
                return new CompositeDisposable(subscriber, controller);
            });
        }

        /// <summary>
        /// Applies paging to the the data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pageController">The page controller.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Page<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] PageController pageController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pageController == null) throw new ArgumentNullException(nameof(pageController));
            return new Pager<T>(source, pageController).Run();
        }

        #endregion


        #region Expiry / size limiter

        /// <summary>
        /// Limits the size of the source cache to the specified limit. 
        /// Notifies which items have been removed from the source list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">sizeLimit cannot be zero</exception>
        /// <exception cref="ArgumentNullException">source</exception>
        /// <exception cref="ArgumentException">sizeLimit cannot be zero</exception>
        public static IObservable<IEnumerable<T>> LimitSizeTo<T>([NotNull] this ISourceList<T> source, int sizeLimit, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sizeLimit <= 0) throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));

            var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? Scheduler.Default);
            var locker = new object();

            return limiter.Run().Synchronize(locker)
                            .Select(toExpire =>
                            {
                                //NB: only expired items are reported so no need to check whether type if removed
                                lock (locker)
                                {
                                    source.Edit(list =>
                                    {
                                        toExpire.ForEach(t => list.Remove(t));
                                    });
                                }
                                //report on expired items
                                return toExpire;
                            });
        }

        /// <summary>
        /// Removes items from the cache according to the value specified by the time selector function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">Selector returning when to expire the item. Return null for non-expiring item</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IEnumerable<T>> ExpireAfter<T>([NotNull] this ISourceList<T> source, [NotNull] Func<T, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Removes items from the cache according to the value specified by the time selector function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">Selector returning when to expire the item. Return null for non-expiring item</param>
        /// <param name="pollingInterval">Enter the polling interval to optimise expiry timers, if ommited 1 timer is created for each unique expiry time</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IEnumerable<T>> ExpireAfter<T>([NotNull] this ISourceList<T> source, [NotNull] Func<T, TimeSpan?> timeSelector, TimeSpan? pollingInterval = null, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));


            var limiter = new ExpireAfter<T>(source, timeSelector, pollingInterval, scheduler ?? Scheduler.Default);
            var locker = new object();

            return limiter.Run().Synchronize(locker)
                            .Select(toExpire =>
                            {
                                //NB: only expired items are reported so no need to check whether type if removed
                                lock (locker)
                                {
                                    source.Edit(innerList =>
                                    {
                                        toExpire.ForEach(t => innerList.Remove(t));
                                    });
                                }
                                //report on expired items
                                return toExpire;
                            });
        }

        #endregion

    }
}
