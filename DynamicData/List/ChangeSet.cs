using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A set of changes which has occured since the last reported change
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    public class ChangeSet<T> : IChangeSet<T>
    {
        private int _adds;
        private int _removes;
        private int _replaced;
        private int _moves;
        private int _refreshes;

        /// <summary>
        /// An empty change set
        /// </summary>
        public static readonly IChangeSet<T> Empty = new ChangeSet<T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
        /// </summary>
        public ChangeSet()
        {
            Items = new List<Change<T>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{T}" /> class.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">items</exception>
        public ChangeSet([NotNull] IEnumerable<Change<T>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var list = items as List<Change<T>> ?? items.ToList();

            Items = list;
            Items.ForEach(change => Add(change, true));
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
        /// Adds the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="countOnly">set to true if the item has already been added</param>
        private void Add(Change<T> item, bool countOnly)
        {
            switch (item.Reason)
            {
                case ListChangeReason.Add:
                    _adds++;
                    break;
                    ;
                case ListChangeReason.AddRange:
                    _adds = _adds + item.Range.Count;
                    break;
                case ListChangeReason.Replace:
                    _replaced++;
                    break;
                    ;
                case ListChangeReason.Remove:
                    _removes++;
                    break;
                case ListChangeReason.RemoveRange:
                    _removes = _removes + item.Range.Count;
                    break;
                case ListChangeReason.Refresh:
                    _removes = _refreshes++;
                    break;
                case ListChangeReason.Moved:
                    _moves++;
                    break;
                case ListChangeReason.Clear:
                    _removes = _removes + item.Range.Count;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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
            get => Items.Capacity;
            set => Items.Capacity = value;
        }

        private List<Change<T>> Items { get; } 

        /// <summary>
        ///     Gets the number of additions
        /// </summary>
        public int Adds => _adds;

        /// <summary>
        ///     Gets the number of updates
        /// </summary>
        public int Replaced => _replaced;

        /// <summary>
        ///     Gets the number of removes
        /// </summary>
        public int Removes => _removes;

        /// <summary>
        ///     Gets the number of removes
        /// </summary>
        public int Refreshes => _refreshes;

        /// <summary>
        ///     Gets the number of requeries
        /// </summary>
        public int Evaluates => 0;

        /// <summary>
        ///     Gets the number of moves
        /// </summary>
        public int Moves => _moves;

        /// <summary>
        ///     The total update count
        /// </summary>
        public int Count => Items.Count;

        /// <summary>
        ///     The total number if individual item changes
        /// </summary>
        public int TotalChanges => Adds + Removes + Replaced + Moves;

        #region Enumeration

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Change<T>> GetEnumerator()
        {
            return Items.GetEnumerator();
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
            return $"ChangeSet<{typeof(T).Name}>. Count={Count}";
        }
    }
}
