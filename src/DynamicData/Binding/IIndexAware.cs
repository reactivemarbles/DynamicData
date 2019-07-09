
namespace DynamicData.Binding
{
    /// <summary>
    /// Implement on an object and use in conjunction with UpdateIndex operator
    /// to make an object aware of it's sorted index
    /// </summary>
    public interface IIndexAware
    {
        /// <summary>
        /// Gets or sets the index.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        int Index { get; set; }
    }
}
