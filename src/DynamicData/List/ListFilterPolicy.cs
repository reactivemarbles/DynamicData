// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Specifies which filter strategy should be used when the filter predicate is changed
    /// </summary>
    public enum ListFilterPolicy
    {
        /// <summary>
        /// Clear all items and replace with matches - optimised for large data sets.
        ///
        /// This option preserves order.
        /// </summary>
        ClearAndReplace,

        /// <summary>
        /// Calculate diff set - optimised for general filtering.
        ///
        /// This option does not preserve order.
        /// </summary>
        CalculateDiff
    }
}