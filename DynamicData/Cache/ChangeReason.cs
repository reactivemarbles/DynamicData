
// ReSharper disable once CheckNamespace

using System;

namespace DynamicData
{
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
        Add = 0,

        /// <summary>
        ///  An item has been updated
        /// </summary>
        Update =1,

        /// <summary>
        ///  An item has removed
        /// </summary>
        Remove =2,

        /// <summary>
        /// Downstream operators will refresh
        /// </summary>
        Refresh =3,

        /// <summary>
        ///   Command to operators to re-evaluate.
        /// </summary>
        [Obsolete("Use ChangeReason.Refresh: The name has changed owning to better semantics")]
        Evaluate = 3,

        /// <summary>
        /// An item has been moved in a sorted collection
        /// </summary>
        Moved = 4,
    }
}
