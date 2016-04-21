namespace DynamicData
{
    /// <summary>
    /// The policy which will be applied when a mutable filter changes
    /// </summary>
    public enum FilterPolicy
    {
        /// <summary>
        /// A full diff-set of adds, updates and removes will be calculated. Use this when objects are mutable.
        /// </summary>
        CalculateDiffSet,

        /// <summary>
        /// Clears the list and inserts batch of items matching the filter. Use this for much better performance
        /// </summary>
        ClearAndReplace
    }
}
