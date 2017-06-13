// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    ///  The reason for an individual change to an observable list
    /// 
    /// Used to signal consumers of any changes to the underlying cache
    /// </summary>
    public enum ListChangeReason
    {
        /// <summary>
        ///  An item has been added
        /// </summary>
        Add,

        /// <summary>
        /// A range of items has been added
        /// </summary>
        AddRange,

        /// <summary>
        ///  An item has been replaced
        /// </summary>
        Replace,

        /// <summary>
        ///  An item has removed
        /// </summary>
        Remove,

        /// <summary>
        /// A range of items has been removed
        /// </summary>
        RemoveRange,

        ///// <summary>
        /////   Command to operators to re-evaluate.
        ///// </summary>
        Refresh,

        /// <summary>
        /// An item has been moved in a sorted collection
        /// </summary>
        Moved,

        /// <summary>
        /// The entire collection has been cleared
        /// </summary>
        Clear,
    }
}
