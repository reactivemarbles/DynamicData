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

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="TObject">The type of the object.</typeparam>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	public struct AggregateItem<TObject, TKey>
    {
		/// <summary>
		/// Initializes a new instance of the <see cref="AggregateItem{TObject, TKey}"/> struct.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="item">The item.</param>
		/// <param name="key">The key.</param>
		public AggregateItem(AggregateType type, TObject item, TKey key)
        {
            Type = type;
            Item = item;
            Key = key;
        }

		/// <summary>
		/// Gets the key.
		/// </summary>
		public TKey Key { get; }

		/// <summary>
		/// Gets the type.
		/// </summary>
		public AggregateType Type { get; }


		/// <summary>
		/// Gets the item.
		/// </summary>
		public TObject Item { get; }
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