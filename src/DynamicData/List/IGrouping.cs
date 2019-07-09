using System.Collections.Generic;

namespace DynamicData.List
{
    /// <summary>
    /// Represents a group which provides an update after any value within the group changes
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    public interface IGrouping<out TObject,  out TGroupKey>
    {
        /// <summary>
        /// Gets the group key
        /// </summary>
        TGroupKey Key { get; }

        /// <summary>
        /// Gets the items.
        /// </summary>
        IEnumerable<TObject> Items { get; }

        /// <summary>
        /// Gets the count.
        /// </summary>
        int Count { get; }

    }
}