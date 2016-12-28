
// ReSharper disable once CheckNamespace
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
