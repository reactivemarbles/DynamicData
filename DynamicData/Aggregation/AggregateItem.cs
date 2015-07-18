namespace DynamicData.Aggregation
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public struct AggregateItem<TObject, TKey>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateItem{TObject, TKey}"/> struct.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        public AggregateItem(AggregateType type, TObject item, TKey key)
        {
            Type = type;
            Item = item;
            Key = key;
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public TKey Key { get; }

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