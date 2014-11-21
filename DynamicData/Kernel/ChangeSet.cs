using System.Collections;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// A set of changes applied to the 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public  class ChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    {
        #region Fields
        
        private readonly List<Change<TObject, TKey>> _items = new List<Change<TObject, TKey>>();
        private int _adds;
        private int _removes;
        private int _evaluates;
        private int _updates;
        private int _moves;

        /// <summary>
        /// An empty change set
        /// </summary>
        public readonly static IChangeSet<TObject, TKey> Empty = new ChangeSet<TObject, TKey>();
       
        #endregion

        #region Construction
        
        public ChangeSet()
        {
        }
        
        public ChangeSet(IEnumerable<Change<TObject, TKey>> items)
        {
            foreach (var update in items)
            {
                Add(update);
            }
        }

        public ChangeSet(Change<TObject, TKey> change)
        {
            Add(change);
        }
        public ChangeSet(ChangeReason reason, TKey key, TObject current)
            : this(reason, key, current, Optional.None<TObject>())
        {
        }

        public ChangeSet(ChangeReason reason,TKey key, TObject current, Optional<TObject> previous)
            : this()
        {
            Add(new Change<TObject, TKey>(reason, key, current, previous));
        }

        public void Add(Change<TObject, TKey> item)
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
        
        private List<Change<TObject, TKey>> Items
        {
            get { return _items; }
        }

        public int Count
        {
            get { return Items.Count; }
        }

        public int Adds
        {
            get { return _adds; }
        }

        public int Updates
        {
            get { return _updates; }
        }

        public int Removes
        {
            get { return _removes; }
        }

        public int Evaluates
        {
            get { return _evaluates; }
        }

        public int Moves
        {
            get { return _moves; }
        }


        #endregion
        
        #region Enumeration
        
        public IEnumerator<Change<TObject, TKey>> GetEnumerator()
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
            return string.Format("ChangeSet<{0}.{1}>. Count={2}", typeof(TObject).Name, typeof(TKey).Name, Count);
        }

    }
}