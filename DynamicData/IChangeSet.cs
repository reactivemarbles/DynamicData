
namespace DynamicData
{
    /// <summary>
    /// Base interface represeting a set of changed
    /// </summary>
    public interface IChangeSet
    {
        /// <summary>
        ///     Gets the number of additions
        /// </summary>
        int Adds { get; }

        /// <summary>
        ///     Gets the number of removes
        /// </summary>
        int Removes { get; }

        /// <summary>
        /// The number of refreshes
        /// </summary>
        int Refreshes { get; }

        /// <summary>
        ///     Gets the number of moves
        /// </summary>
        int Moves { get; }

        /// <summary>
        ///     The total update count
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets or sets the capacity of the change set
        /// </summary>
        /// <value>
        /// The capacity.
        /// </value>
        int Capacity { get; set; }
    }
}
