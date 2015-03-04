using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{

    /// <summary>
    /// The type of aggregation
    /// </summary>
    public enum AggregateType
    {
        /// <summary>
        /// The add
        /// </summary>
        Add,
        /// <summary>
        /// The remove
        /// </summary>
        Remove
    }

    public struct AggregateItem<TObject, TKey>
    {
 
        private readonly AggregateType _type;
        private readonly TObject _item;
        private readonly TKey _key;
        
        public AggregateItem(AggregateType type, TObject item, TKey key)
        {
            _type = type;
            _item = item;
            _key = key;
        }

        public TKey Key
        {
            get { return _key; }
        }

        public AggregateType Type
        {
            get { return _type; }
        }


        public TObject Item
        {
            get { return _item; }
        }

    }


    /// <summary>
    ///  The reason for an individual change.  
    /// 
    /// Used to signal consumers of any changes to the underlying cache
    /// </summary>
    public enum ChangeReason
    {
        /// <summary>
        ///  An item has been added
        /// </summary>
        Add,

        /// <summary>
        ///  An item has been updated
        /// </summary>
        Update,

        /// <summary>
        ///  An item has removed
        /// </summary>
        Remove,

        /// <summary>
        ///   Command to operators to re-evaluate.
        /// </summary>
        Evaluate,

        /// <summary>
        /// An item has been moved in a sorted collection
        /// </summary>
        Moved,
    }
}