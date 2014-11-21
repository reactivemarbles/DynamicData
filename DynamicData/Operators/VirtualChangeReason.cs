namespace DynamicData.Operators
{
    /// <summary>
    /// The reason for a virtual update
    /// </summary>
    public enum VirtualChangeReason
    {
        /// <summary>
        /// Underlying data has changed
        /// </summary>
        Updated,

        /// <summary>
        /// The consumer called into the stream with new virtual parameters
        /// </summary>
        Virtualised
    }
}