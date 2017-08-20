
namespace DynamicData.PLinq
{
    /// <summary>
    /// The type of parallisation
    /// </summary>
    public enum ParallelType
    {
        /// <summary>
        /// No parallelisation will take place
        /// </summary>
        None,

        /// <summary>
        /// Parallelisation will take place without preserving the enumerable order
        /// </summary>
        Parallelise,

        /// <summary>
        /// Parallelisation will take place whilst preserving the enumerable order
        /// </summary>
        Ordered
    }
}
