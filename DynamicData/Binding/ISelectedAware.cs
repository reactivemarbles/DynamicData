namespace DynamicData.Binding
{
    /// <summary>
    /// When implemented, the binding infrastructure will set whether the object is selected
    /// </summary>
    public interface ISelectedAware
    {
        /// <summary>
        /// Gets or sets the index.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        bool IsSelected { get; set; }
    }
}