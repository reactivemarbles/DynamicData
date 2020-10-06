
// ReSharper disable once CheckNamespace

using System.Diagnostics.CodeAnalysis;

namespace DynamicData
{
    /// <summary>
    /// Options for sorting
    /// </summary>
    [SuppressMessage("Design", "CA1717: Only flags should have plural names", Justification = "Backwards compatibility")]
    public enum SortOptions
    {
        /// <summary>
        /// No sort options are specified.
        /// </summary>
        None,

        /// <summary>
        /// Use binary search to locate item index.
        /// </summary>
        UseBinarySearch
    }
}
