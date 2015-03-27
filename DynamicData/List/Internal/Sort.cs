using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class Sort<T>
	{
		private readonly IObservable<IChangeSet<T>> _source;
		private readonly IComparer<T> _comparer;
		private readonly SortOptions _sortOptions;
		private readonly ChangeAwareList<T> _list = new ChangeAwareList<T>();

		public Sort(IObservable<IChangeSet<T>> source, IComparer<T> comparer, SortOptions sortOptions)
		{
			_source = source;
			_comparer = comparer;
			_sortOptions = sortOptions;
		}

		public IObservable<IChangeSet<T>> Run()
		{
			return _source.Select(Process);
		}

		private IChangeSet<T> Process(IChangeSet<T> changes)
		{
			changes.ForEach(change =>
			{

				switch (change.Reason)
				{
					case ListChangeReason.Add:
					{
						var current = change.Item.Current;
						Insert(current);
						break;
					}
					case ListChangeReason.AddRange:
					{
						change.Range.ForEach(Insert);
						break;
					}
					case ListChangeReason.Update:
					{
						var current = change.Item.Current;
						//TODO: check whether an item should stay in the same position
						//i.e. update and move
						Remove(change.Item.Previous.Value);
						Insert(current);
						break;
					}
					case ListChangeReason.Remove:
					{
						var current = change.Item.Current;
						Remove(current);
						break;
					}
					case ListChangeReason.RemoveRange:
						{
							change.Range.ForEach(Remove);
							break;
						}
					case ListChangeReason.Clear:
					{
						_list.Clear();
                        break;
					}

				}
			});

			return _list.CaptureChanges();
		}



		private void Remove(T item)
		{
			var index = GetCurrentPosition(item);
			_list.RemoveAt(index);

		}

		private void Insert(T item)
		{
			var index = GetInsertPosition(item);
			_list.Insert(index,item);

		}

		private int GetInsertPosition(T item)
		{
			return _sortOptions == SortOptions.UseBinarySearch
				? GetInsertPositionBinary(item)
				: GetInsertPositionLinear(item);
		}

		private int GetInsertPositionLinear(T item)
		{
			for (int i = 0; i < _list.Count; i++)
			{
				if (_comparer.Compare(item, _list[i]) < 0)
					return i;
			}
			return _list.Count;
		}

		private int GetInsertPositionBinary(T item)
		{
			int index = _list.BinarySearch(item, _comparer);
			int insertIndex = ~index;

			//sort is not returning uniqueness
			if (insertIndex < 0)
				throw new SortException("Binary search has been specified, yet the sort does not yeild uniqueness");
			return insertIndex;
		}

		private int GetCurrentPosition(T item)
		{
			int index = _sortOptions == SortOptions.UseBinarySearch
				? _list.BinarySearch(item,_comparer)
				: _list.IndexOf(item);

			if (index < 0)
				throw new SortException("Current item cannot be found");

			return index;
		}
	}
}