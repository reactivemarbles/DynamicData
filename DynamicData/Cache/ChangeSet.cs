using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A collection of changes
    /// </summary>
    public class ChangeSet<TObject, TKey> : List<Change<TObject, TKey>>, IChangeSet<TObject, TKey>
    {
        /// <summary>
        /// An empty change set
        /// </summary>
        public static readonly ChangeSet<TObject, TKey> Empty = new ChangeSet<TObject, TKey>();

        /// <inheritdoc />
        public ChangeSet()
        {
        }

        /// <inheritdoc />
        public ChangeSet(IEnumerable<Change<TObject, TKey>> collection) 
            : base(collection)
        {
        }

        /// <inheritdoc />
        public ChangeSet(int capacity) : base(capacity)
        {
        }



        /// <inheritdoc />
        public int Adds => this.Count(c => c.Reason == ChangeReason.Add);

        /// <inheritdoc />
        public int Updates => this.Count(c => c.Reason == ChangeReason.Update);

        /// <inheritdoc />
        public int Removes => this.Count(c => c.Reason == ChangeReason.Remove);

        /// <inheritdoc />
        public int Refreshes => this.Count(c => c.Reason == ChangeReason.Refresh);

        /// <inheritdoc />
        public int Evaluates => this.Count(c => c.Reason == ChangeReason.Refresh);

        /// <inheritdoc />
        public int Moves => this.Count(c => c.Reason == ChangeReason.Moved);


        /// <inheritdoc />
        public override string ToString()
        {
            return $"ChangeSet<{typeof(TObject).Name}.{typeof(TKey).Name}>. Count={Count}";
        }
    }
}
