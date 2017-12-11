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
    /// A collection of changes
    /// </summary>
    public class ChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    {
        private List<Change<TObject, TKey>> Items { get; } 

        /// <summary>
        /// An empty change set
        /// </summary>
        public static readonly IChangeSet<TObject, TKey> Empty = new ChangeSet<TObject, TKey>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        public ChangeSet()
        {
            Items = new List<Change<TObject, TKey>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="items">The items.</param>
        public ChangeSet([NotNull] IEnumerable<Change<TObject, TKey>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Items = new List<Change<TObject, TKey>>(items);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        public ChangeSet(ChangeReason reason, TKey key, TObject current)
            : this(reason, key, current, Optional.None<TObject>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="previous">The previous.</param>
        private ChangeSet(ChangeReason reason, TKey key, TObject current, Optional<TObject> previous)
        {
            Items = new List<Change<TObject, TKey>>
            {
                new Change<TObject, TKey>(reason, key, current, previous)
            };
        }

        /// <inheritdoc />
        public int Capacity
        {
            get => Items.Capacity;
            set => Items.Capacity = value;
        }

        /// <inheritdoc />
        public int Count => Items.Count;

        /// <inheritdoc />
        public int Adds => Items.Count(c => c.Reason == ChangeReason.Add);

        /// <inheritdoc />
        public int Updates => Items.Count(c => c.Reason == ChangeReason.Update);

        /// <inheritdoc />
        public int Removes => Items.Count(c => c.Reason == ChangeReason.Remove);

        /// <inheritdoc />
        public int Refreshes => Items.Count(c => c.Reason == ChangeReason.Refresh);

        /// <inheritdoc />
        public int Evaluates => Items.Count(c => c.Reason == ChangeReason.Refresh);

        /// <inheritdoc />
        public int Moves => Items.Count(c => c.Reason == ChangeReason.Moved);


        /// <inheritdoc />
        public IEnumerator<Change<TObject, TKey>> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        /// <inheritdoc />
        public override string ToString()
        {
            return $"ChangeSet<{typeof(TObject).Name}.{typeof(TKey).Name}>. Count={Count}";
        }
    }
}
