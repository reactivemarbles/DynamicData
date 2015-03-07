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

		/// <summary>
		/// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
		/// </summary>
		public ChangeSet()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
		/// </summary>
		/// <param name="items">The items.</param>
		public ChangeSet(IEnumerable<Change<T>> items)
		{
			foreach (var update in items)
			{
				Add(update);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
		/// </summary>
		/// <param name="reason">The reason.</param>
		/// <param name="current">The current.</param>
		/// <param name="previous">The previous.</param>
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


		/// <summary>
		///     Gets the number of additions
		/// </summary>
		public int Adds => _adds;

		/// <summary>
		///     Gets the number of updates
		/// </summary>
		public int Updates => _updates;
		/// <summary>
		///     Gets the number of removes
		/// </summary>
		public int Removes => _removes;
	
		/// <summary>
		///     Gets the number of requeries
		/// </summary>
		public int Evaluates => _evaluates;

		/// <summary>
		///     Gets the number of moves
		/// </summary>
		public int Moves => _moves;

		/// <summary>
		///     The total update count
		/// </summary>
		public int Count => Items.Count;
		#endregion

		#region Enumeration

		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<Change<T>> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}


		#endregion

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			return string.Format("ChangeSet<{0}>. Count={1}", typeof(T).Name,
				Count);
		}

	}
}