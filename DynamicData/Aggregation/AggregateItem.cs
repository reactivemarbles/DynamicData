
namespace DynamicData.Aggregation
{
    /// <summary>
    /// An object representing added and removed items in a continuous aggregation stream
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    public readonly struct AggregateItem<TObject>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateItem{TObject}"/> struct.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="item">The item.</param>
        public AggregateItem(AggregateType type, TObject item)
        {
            Type = type;
            Item = item;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public AggregateType Type { get; }

        /// <summary>
        /// Gets the item.
        /// </summary>
        public TObject Item { get; }
    }
}
