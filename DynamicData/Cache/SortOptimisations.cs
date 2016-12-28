using System;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Flags used to specify one or more sort optimisations
    /// </summary>
    [Flags]
    public enum SortOptimisations
    {
        /// <summary>
        /// No sorting optimisation are applied
        /// </summary>
        None = 0,

        /// <summary>
        /// Specify this option if the comparer used for sorting compares immutable fields only.
        /// In which case index changes can be calculated using BinarySearch rather than the expensive IndexOf
        /// </summary>
        ComparesImmutableValuesOnly = 1,

        /// <summary>
        /// Ignores moves because of evaluates.  
        /// Use for virtualisatiom or pagination
        /// </summary>
        IgnoreEvaluates = 2,

        /// <summary>
        /// The insert at end then sort entire set.  This can be the best algorthm for large datasets with many changes
        /// </summary>
        InsertAtEndThenSort = 3
    }
}
