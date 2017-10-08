// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Specifies which filter strategy should be used when the filter predicate is changed
    /// </summary>
    public enum ListFilterPolicy
    {
        /// <summary>
        /// Clear all items and replace with matches - optimised for large data sets
        /// </summary>
        ClearAndReplace,

        /// <summary>
        /// Caclulate diff set - optimisied for generaral filtering.
        /// </summary>
        CalculateDiff
    }
}