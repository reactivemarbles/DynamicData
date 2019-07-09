using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A collection of changes.
    /// 
    /// Changes are always published in the order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    public interface IChangeSet<TObject> : IEnumerable<Change<TObject>>, IChangeSet
    {
        /// <summary>
        ///     Gets the number of updates
        /// </summary>
        int Replaced { get; }

        /// <summary>
        /// The total count of items changed
        /// </summary>
        int TotalChanges { get; }
    }
}
