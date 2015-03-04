using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using DynamicData.List;

namespace DynamicData
{
	/// <summary>
	/// An observable list
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	internal sealed class SourceList<T> : ISourceList<T>
	{
		private readonly ISubject<IChangeSet<T>> _changes = new Subject<IChangeSet<T>>();
		private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
		private readonly ReaderWriter<T> _readerWriter;
		private readonly IDisposable _disposer;
		private readonly object _locker = new object();

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
				.FinallySafe(_changes.OnCompleted)
				.Subscribe(changes => _readerWriter.Write(changes)
					.Then(InvokeNext, _changes.OnError));

		}

		public void Edit(Action<IList<T>> updateAction)
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

		public Optional<ItemWithIndex<T>> Lookup(T item, IEqualityComparer<T> equalityComparer = null)
		{
			return _readerWriter.Items.Lookup(item, equalityComparer);
		}

		public IEnumerable<T> Items => _readerWriter.Items;

		public int Count => _readerWriter.Count;


		public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();


		public IObservable<IChangeSet<T>> Connect()
		{
			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						lock (_locker)
						{
							var initial = GetInitialUpdates();
							if (initial.Count > 0) observer.OnNext(initial);

							return _changes.FinallySafe(observer.OnCompleted)
								.SubscribeSafe(observer);
						}
					});
		}


		internal IChangeSet<T> GetInitialUpdates()
		{
			var initial = _readerWriter.Items.WithIndex().Select(t => new Change<T>(ChangeReason.Add, t.Item, t.Index));
			return new ChangeSet<T>(initial);
		}

		public void Dispose()
		{
			_disposer.Dispose();
		}
	}
}