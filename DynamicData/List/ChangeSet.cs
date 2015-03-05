using System.Collections;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// A set of changes which has occured since the last reported changes
	/// </summary>
	/// <typeparam name="T">The type of the object.</typeparam>
	public class ChangeSet<T> : IChangeSet<T>
	{
		#region Fields

		private readonly List<Change<T>> _items = new List<Change<T>>();
		private int _adds;
		private int _removes;
		private int _evaluates;
		private int _updates;
		private int _moves;

		/// <summary>
		/// An empty change set
		/// </summary>
		public readonly static IChangeSet<T> Empty = new ChangeSet<T>();

		#endregion

		#region Construction

		public ChangeSet()
		{
		}

		public ChangeSet(IEnumerable<Change<T>> items)
		{
			foreach (var update in items)
			{
				Add(update);
			}
		}

		public ChangeSet(ChangeReason reason, T current, Optional<T> previous)
			: this()
		{
			Add(new Change<T>(reason, current, previous));
		}

		public void Add(Change<T> item)
		{
			switch (item.Reason)
			{
				case ChangeReason.Add:
					_adds++;
					break;
				case ChangeReason.Update:
					_updates++;
					break;
				case ChangeReason.Remove:
					_removes++;
					break;
				case ChangeReason.Evaluate:
					_evaluates++;
					break;
				case ChangeReason.Moved:
					_moves++;
					break;
			}
			_items.Add(item);
		}


		#endregion

		#region Properties

		private List<Change<T>> Items => _items;

		public int Count => Items.Count;

		public int Adds => _adds;

		public int Updates => _updates;

		public int Removes => _removes;

		public int Evaluates => _evaluates;

		public int Moves => _moves;

		#endregion

		#region Enumeration

		public IEnumerator<Change<T>> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		#endregion

		public override string ToString()
		{
			return string.Format("ChangeSet<{0}>. Count={1}", typeof(T).Name,
				Count);
		}

	}
}