using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Annotations;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// An editable observable list
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	public sealed class SourceList<T> : ISourceList<T>
	{
		private readonly ISubject<IChangeSet<T>> _changes = new Subject<IChangeSet<T>>();
		private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
		private readonly ReaderWriter<T> _readerWriter;
		private readonly IDisposable _disposer;
		private readonly object _locker = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="SourceList{T}"/> class.
		/// </summary>
		/// <param name="source">The source.</param>
		public SourceList(IObservable<IChangeSet<T>> source = null)
		{
			_readerWriter = new ReaderWriter<T>();

			var loader = source == null ? Disposable.Empty : LoadFromSource(source);

			_disposer = Disposable.Create(() =>
			{
				loader.Dispose();
				_changes.OnCompleted();
				if (_countChanged.IsValueCreated)
					_countChanged.Value.OnCompleted();
			});
		}

		private IDisposable LoadFromSource(IObservable<IChangeSet<T>> source)
		{
			return source.Synchronize(_locker)
				.Subscribe(changes => _readerWriter.Write(changes)
					.Then(InvokeNext, _changes.OnError),()=> _changes.OnCompleted());

		}
		/// <summary>
		/// Edit the inner list within the list's internal locking mechanism
		/// </summary>
		/// <param name="updateAction">The update action.</param>
		public void Edit([NotNull] Action<IList<T>> updateAction)
		{
			if (updateAction == null) throw new ArgumentNullException("updateAction");
			
			_readerWriter.Write(updateAction)
				.Then(InvokeNext, _changes.OnError);
		}

		private void InvokeNext(IChangeSet<T> changes)
		{
			if (changes.Count == 0) return;

			lock (_locker)
			{
				try
				{
					_changes.OnNext(changes);

					if (_countChanged.IsValueCreated)
						_countChanged.Value.OnNext(_readerWriter.Count);

				}
				catch (Exception ex)
				{
					_changes.OnError(ex);
				}
			}
		}

		/// <summary>
		/// Lookups the item using the specified equality comparer
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="equalityComparer">The equality comparer.</param>
		/// <returns>
		/// An ItemWithIndex container which contains the item with it's index
		/// </returns>
		public Optional<ItemWithIndex<T>> Lookup(T item, IEqualityComparer<T> equalityComparer = null)
		{
			return _readerWriter.Lookup(item, equalityComparer);
		}

		/// <summary>
		/// Gets or sets the items.
		/// </summary>
		/// <value>
		/// The items.
		/// </value>
		public IEnumerable<T> Items => _readerWriter.Items;

		/// <summary>
		/// Gets or sets the count.
		/// </summary>
		/// <value>
		/// The count.
		/// </value>
		public int Count => _readerWriter.Count;


		/// <summary>
		/// Gets or sets the count changed.
		/// </summary>
		/// <value>
		/// The count changed.
		/// </value>
		public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();


		/// <summary>
		/// Connects using the specified predicate.
		/// </summary>
		/// <param name="predicate">The predicate.</param>
		/// <returns></returns>
		public IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate = null)
		{
			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						lock (_locker)
						{
							var initial = GetInitialUpdates(predicate);
							if (initial.Count > 0) observer.OnNext(initial);
							var source = _changes.FinallySafe(observer.OnCompleted);

							if (predicate != null)
								source = source.Filter(predicate);

							return source.SubscribeSafe(observer);
						}
					});
		}

		private IChangeSet<T> GetInitialUpdates(Func<T, bool> predicate = null)
		{
			var items = predicate == null
				? _readerWriter.Items
				: _readerWriter.Items.Where(predicate);

			var initial = items.WithIndex().Select(t => new Change<T>(ListChangeReason.Add, t.Item, t.Index));
			return new ChangeSet<T>(initial);
		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		public void Dispose()
		{
			_disposer.Dispose();
		}
	}
}
