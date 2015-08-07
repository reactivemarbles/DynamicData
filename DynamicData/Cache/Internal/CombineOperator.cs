

namespace DynamicData.Internal
{
    /// <summary>
    /// How the multiple streams are combinedL
    /// </summary>
    public enum CombineOperator
    {
        /// <summary>
        /// Resultant stream is comprised of items which are in each the caches
        /// </summary>
        ContainedInEach,

        /// <summary>
        /// Resultant stream is comprised of items which are in any of the caches
        /// </summary>
        ContainedInAny,

        /// <summary>
        /// Resultant stream is comprised of items which are in onlys the caches
        /// </summary>
        ContainedInOne,

        /// <summary>
        /// Resultant stream is comprised of items which are in the first stream and not the others
        /// </summary>
        ExceptFor
    }
}