using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
			_items =  items.ToList();
			_items.ForEach(change => Add(change, true));
		}

		/// <summary>
		/// Adds the specified item.
		/// </summary>
		/// <param name="item">The item.</param>
		public void Add(Change<T> item)
		{
			Add(item, false);
		}

		/// <summary>
		/// Adds the specified items. 
		/// </summary>
		/// <param name="items">The items.</param>
		public void AddRange(IEnumerable<Change<T>> items)
		{
			var enumerable = items as ICollection<Change<T>> ?? items.ToList();
			_items.AddRange(enumerable);

			_items.ForEach(t =>
			{
				Add(t,true);
			});

		}

		/// <summary>
		/// Adds the specified item.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="countOnly">set to true if the item has already been added</param>
		private void Add(Change<T> item, bool countOnly)
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
			if (!countOnly) Items.Add(item);
		}


		/// <summary>
		/// Gets or sets the capacity.
		/// </summary>
		/// <value>
		/// The capacity.
		/// </value>
		public int Capacity
		{
			get { return _items.Capacity; }
			set { _items.Capacity = value; }
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